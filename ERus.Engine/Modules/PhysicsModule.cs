using ERus.Engine.Core;
using Silk.NET.Maths;
using Jitter2;
using Jitter2.Dynamics;
using Jitter2.LinearMath;

namespace ERus.Engine.Modules;

public class PhysicsModule : IEngineModule
{
    public World PhysicsWorld { get; private set; }

    public void Initialize(Engine.Core.Engine engine)
    {
        PhysicsWorld = new World();
        // Gravidade padrão
        PhysicsWorld.Gravity = new JVector(0, -9.81f, 0);
    }

    public void Update(double deltaTime)
    {
        // O PhysicsSystem ficará encarregado de rodar PhysicsWorld.Step(...) para manter o fluxo do ECS.
    }

    public void Render(double deltaTime)
    {
    }

    public bool Raycast(Vector3D<float> origin, Vector3D<float> direction, out RigidBody? hitBody, out Vector3D<float> hitNormal, out float hitFraction)
    {
        JVector jOrigin = new JVector(origin.X, origin.Y, origin.Z);
        JVector jDir = new JVector(direction.X, direction.Y, direction.Z);
        
        bool hit = PhysicsWorld.DynamicTree.RayCast(jOrigin, jDir, null, null, out Jitter2.Collision.IDynamicTreeProxy? proxy, out JVector normal, out float fraction);
        
        hitBody = null;
        hitNormal = new Vector3D<float>(normal.X, normal.Y, normal.Z);
        hitFraction = fraction;

        if (hit && proxy is Jitter2.Collision.Shapes.RigidBodyShape shape)
        {
            hitBody = shape.RigidBody;
            return true;
        }

        return hit;
    }

    public void Dispose()
    {
        PhysicsWorld.Clear();
    }
}
