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

    public bool Filter(RigidBodyShape shapeA, RigidBodyShape shapeB, ref JVector point1, ref JVector point2, ref JVector normal, ref float penetration)
    {
        if (shapeA.RigidBody?.Tag is int idA && shapeB.RigidBody?.Tag is int idB)
        {
            bool isTrigger = false;
            
            var entityA = new Entity(idA);
            var entityB = new Entity(idB);
            
            if (_registry.HasComponent<ColliderComponent>(entityA))
            {
                if (_registry.GetComponent<ColliderComponent>(entityA).IsTrigger) isTrigger = true;
            }
            if (_registry.HasComponent<ColliderComponent>(entityB))
            {
                if (_registry.GetComponent<ColliderComponent>(entityB).IsTrigger) isTrigger = true;
            }

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

public class PhysicsSystem : BaseSystem
{
    private PhysicsModule? _physicsModule;
    private Core.Engine _engine;
    private bool _filterInitialized = false;
    
    public Queue<CollisionEvent> CollisionEvents { get; private set; } = new Queue<CollisionEvent>();

    public PhysicsSystem(Registry registry, Core.Engine engine) : base(registry)
    {
        _engine = engine;
    }

    public override void Update(double deltaTime)
    {
        if (_physicsModule == null)
        {
            _physicsModule = _engine.GetModule<PhysicsModule>();
            if (_physicsModule == null) return;
        }

        if (!_filterInitialized)
        {
            _physicsModule.PhysicsWorld.NarrowPhaseFilter = new ERusNarrowPhaseFilter(Registry, CollisionEvents);
            _filterInitialized = true;
        }

        foreach (var entity in Registry.View<RigidBodyComponent, TransformComponent>())
        {
            ref var rb = ref Registry.GetComponent<RigidBodyComponent>(entity);
            ref var transform = ref Registry.GetComponent<TransformComponent>(entity);
            
            if (rb.InternalBody == null)
            {
                var body = _physicsModule.PhysicsWorld.CreateRigidBody();
                body.Tag = entity.Id; // Tag = Entity ID
                
                if (Registry.HasComponent<ColliderComponent>(entity))
                {
                    ref var collider = ref Registry.GetComponent<ColliderComponent>(entity);
                    if (collider.Shape == ColliderShape.Box)
                    {
                        body.AddShape(new BoxShape(collider.Size.X, collider.Size.Y, collider.Size.Z));
                    }
                    else if (collider.Shape == ColliderShape.Sphere)
                    {
                        body.AddShape(new SphereShape(collider.Size.X));
                    }
                    else if (collider.Shape == ColliderShape.Capsule)
                    {
                        body.AddShape(new CapsuleShape(collider.Size.X, collider.Size.Y));
                    }
                    
                    body.Friction = collider.Friction;
                    body.Restitution = collider.Restitution;
                }

                body.Position = new JVector(transform.Position.X, transform.Position.Y, transform.Position.Z);
                body.SetMassInertia(rb.Mass);
                body.MotionType = rb.IsKinematic ? MotionType.Kinematic : MotionType.Dynamic;
                
                // Aplicar travamento de eixos (Constraints na Inércia)
                if (rb.FreezeRotationX || rb.FreezeRotationY || rb.FreezeRotationZ)
                {
                    JMatrix invInertia = body.InverseInertia;
                    if (rb.FreezeRotationX) invInertia.M11 = 0;
                    if (rb.FreezeRotationY) invInertia.M22 = 0;
                    if (rb.FreezeRotationZ) invInertia.M33 = 0;
                    body.SetMassInertia(invInertia, rb.Mass, true);
                }
                
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
        
        // Sincronizar Joints
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
                        // Criar uma BallSocket (Base universal para simular Hinge/Fixed se parametrizado depois)
                        var constraint = _physicsModule.PhysicsWorld.CreateConstraint<Jitter2.Dynamics.Constraints.BallSocket>(body1, body2);
                        constraint.Initialize(body1.Position); // Pivô no meio do body1
                        joint.InternalConstraint = constraint;
                    }
                }
            }
        }

        _physicsModule.PhysicsWorld.Step((float)deltaTime);

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
        
        CollisionEvents.Clear();
    }
}
