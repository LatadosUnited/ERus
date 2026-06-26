using ERus.Engine.Core;
using ERus.Engine.Modules;
using Silk.NET.Maths;
using Jitter2.Dynamics;
using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using Jitter2.Collision;
using System.Collections.Generic;

namespace ERus.Engine.ECS;

// Implementação do Filtro para Triggers e Callbacks
public class ERusNarrowPhaseFilter : INarrowPhaseFilter
{
    private Registry _registry;
    private Queue<CollisionEvent> _events;

    public ERusNarrowPhaseFilter(Registry registry, Queue<CollisionEvent> events)
    {
        _registry = registry;
        _events = events;
    }

    private bool CheckIsTrigger(Entity entity)
    {
        if (_registry.HasComponent<BoxColliderComponent>(entity) && _registry.GetComponent<BoxColliderComponent>(entity).IsTrigger) return true;
        if (_registry.HasComponent<SphereColliderComponent>(entity) && _registry.GetComponent<SphereColliderComponent>(entity).IsTrigger) return true;
        if (_registry.HasComponent<CapsuleColliderComponent>(entity) && _registry.GetComponent<CapsuleColliderComponent>(entity).IsTrigger) return true;
        if (_registry.HasComponent<CylinderColliderComponent>(entity) && _registry.GetComponent<CylinderColliderComponent>(entity).IsTrigger) return true;
        if (_registry.HasComponent<MeshColliderComponent>(entity) && _registry.GetComponent<MeshColliderComponent>(entity).IsTrigger) return true;
        return false;
    }

    public bool Filter(RigidBodyShape shapeA, RigidBodyShape shapeB, ref JVector point1, ref JVector point2, ref JVector normal, ref float penetration)
    {
        if (shapeA.RigidBody?.Tag is int idA && shapeB.RigidBody?.Tag is int idB)
        {
            var entityA = new Entity(idA);
            var entityB = new Entity(idB);
            
            bool isTrigger = CheckIsTrigger(entityA) || CheckIsTrigger(entityB);

            // Gerar o evento de colisão/trigger
            _events.Enqueue(new CollisionEvent
            {
                EntityA = idA,
                EntityB = idB,
                IsTriggerEvent = isTrigger,
                ImpactPoint = new Vector3D<float>(point1.X, point1.Y, point1.Z),
                Normal = new Vector3D<float>(normal.X, normal.Y, normal.Z)
            });

            // Retorna false se for trigger para o Jitter2 não gerar força de repulsão
            return !isTrigger;
        }
        return true;
    }
}

/// <summary>
/// Sistema de física do ECS. Orquestra:
/// 1. Criação/configuração de RigidBodies a partir de componentes
/// 2. Execução do step de simulação
/// 3. Sincronização dos resultados de volta para os TransformComponents
/// 
/// A criação de shapes foi delegada para PhysicsShapeFactory.
/// </summary>
public class PhysicsSystem : BaseSystem
{
    private readonly PhysicsModule _physicsModule;
    private Core.Engine _engine;
    private bool _filterInitialized = false;
    
    private readonly Queue<CollisionEvent> _collisionEvents = new Queue<CollisionEvent>();

    public PhysicsSystem(Registry registry, Core.Engine engine, PhysicsModule physicsModule) : base(registry)
    {
        _engine = engine;
        _physicsModule = physicsModule;
    }

    public override void Update(double deltaTime)
    {

        if (!_filterInitialized)
        {
            _physicsModule.PhysicsWorld.NarrowPhaseFilter = new ERusNarrowPhaseFilter(Registry, _collisionEvents);
            _filterInitialized = true;
        }

        // --- Fase 1: Criação e configuração de RigidBodies ---
        foreach (var entity in Registry.View<RigidBodyComponent, TransformComponent>())
        {
            ref var rb = ref Registry.GetComponent<RigidBodyComponent>(entity);
            ref var transform = ref Registry.GetComponent<TransformComponent>(entity);
            
            if (rb.InternalBody == null)
            {
                var body = _physicsModule.PhysicsWorld.CreateRigidBody();
                body.Tag = entity.Id;
                
                var globalTransform = PhysicsShapeFactory.GetGlobalTransform(entity, Registry);
                System.Numerics.Matrix4x4.Invert(globalTransform, out var parentInverse);

                PhysicsShapeFactory.AddShapesFromEntity(entity, body, parentInverse, Registry, _engine);
                
                // TODO: Obter PhysicsMaterial real
                body.Friction = 0.5f;
                body.Restitution = 0.0f;

                body.Position = new JVector(transform.Position.X, transform.Position.Y, transform.Position.Z);
                body.SetMassInertia(rb.Mass);
                body.MotionType = rb.IsKinematic ? MotionType.Kinematic : MotionType.Dynamic;
                
                body.AffectedByGravity = rb.UseGravity;
                
                ApplyConstraints(body, rb);
                
                rb.InternalBody = body;
            }
            else
            {
                var body = (RigidBody)rb.InternalBody;
                
                if (transform.IsDirty)
                {
                    body.Position = new JVector(transform.Position.X, transform.Position.Y, transform.Position.Z);
                    transform.IsDirty = false;
                }
                
                body.MotionType = rb.IsKinematic ? MotionType.Kinematic : MotionType.Dynamic;
            }
        }
        
        // --- Fase 2: Sincronizar Joints ---
        foreach (var entity in Registry.View<JointComponent, RigidBodyComponent>())
        {
            ref var joint = ref Registry.GetComponent<JointComponent>(entity);
            ref var rb = ref Registry.GetComponent<RigidBodyComponent>(entity);
            
            if (joint.InternalConstraint == null && joint.TargetEntityId != -1)
            {
                var targetEntity = new Entity(joint.TargetEntityId);
                if (Registry.HasComponent<RigidBodyComponent>(targetEntity))
                {
                    ref var targetRb = ref Registry.GetComponent<RigidBodyComponent>(targetEntity);
                    var body1 = rb.InternalBody as RigidBody;
                    var body2 = targetRb.InternalBody as RigidBody;
                    
                    if (body1 != null && body2 != null)
                    {
                        var constraint = _physicsModule.PhysicsWorld.CreateConstraint<Jitter2.Dynamics.Constraints.BallSocket>(body1, body2);
                        constraint.Initialize(body1.Position); 
                        joint.InternalConstraint = constraint;
                    }
                }
            }
        }

        // --- Fase 3: Step da simulação e sincronização ---
        if (_engine.State == EngineState.Play)
        {
            _physicsModule.PhysicsWorld.Step((float)deltaTime);

            while (_collisionEvents.TryDequeue(out var evt))
            {
                _engine.EventBus.Publish(evt);
            }

            foreach (var entity in Registry.View<RigidBodyComponent, TransformComponent>())
            {
                ref var rb = ref Registry.GetComponent<RigidBodyComponent>(entity);
                if (rb.InternalBody == null || rb.IsKinematic) continue;

                var body = (RigidBody)rb.InternalBody;
                ref var transform = ref Registry.GetComponent<TransformComponent>(entity);

                transform.Position = new Vector3D<float>(body.Position.X, body.Position.Y, body.Position.Z);
                
                rb.LinearVelocity = new Vector3D<float>(body.Velocity.X, body.Velocity.Y, body.Velocity.Z);
                rb.AngularVelocity = new Vector3D<float>(body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z);
            }
        }
        else
        {
            _collisionEvents.Clear();
        }
    }

    private void ApplyConstraints(RigidBody body, RigidBodyComponent rb)
    {
        if ((rb.Constraints & RigidbodyConstraints.FreezeRotation) != 0)
        {
            JMatrix invInertia = body.InverseInertia;
            if ((rb.Constraints & RigidbodyConstraints.FreezeRotationX) != 0) invInertia.M11 = 0;
            if ((rb.Constraints & RigidbodyConstraints.FreezeRotationY) != 0) invInertia.M22 = 0;
            if ((rb.Constraints & RigidbodyConstraints.FreezeRotationZ) != 0) invInertia.M33 = 0;
            body.SetMassInertia(invInertia, rb.Mass, true);
        }
        
        if ((rb.Constraints & RigidbodyConstraints.FreezePosition) == RigidbodyConstraints.FreezePosition)
        {
            var constraint = _physicsModule!.PhysicsWorld.CreateConstraint<Jitter2.Dynamics.Constraints.BallSocket>(body, null);
            constraint.Initialize(body.Position);
        }
        else
        {
            if ((rb.Constraints & RigidbodyConstraints.FreezePositionX) != 0) {
                var constraint = _physicsModule!.PhysicsWorld.CreateConstraint<Jitter2.Dynamics.Constraints.PointOnPlane>(body, null);
                constraint.Initialize(JVector.UnitX, body.Position, body.Position);
            }
            if ((rb.Constraints & RigidbodyConstraints.FreezePositionY) != 0) {
                var constraint = _physicsModule!.PhysicsWorld.CreateConstraint<Jitter2.Dynamics.Constraints.PointOnPlane>(body, null);
                constraint.Initialize(JVector.UnitY, body.Position, body.Position);
            }
            if ((rb.Constraints & RigidbodyConstraints.FreezePositionZ) != 0) {
                var constraint = _physicsModule!.PhysicsWorld.CreateConstraint<Jitter2.Dynamics.Constraints.PointOnPlane>(body, null);
                constraint.Initialize(JVector.UnitZ, body.Position, body.Position);
            }
        }
    }
}
