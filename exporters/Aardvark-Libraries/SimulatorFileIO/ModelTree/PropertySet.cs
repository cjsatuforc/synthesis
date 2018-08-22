﻿/// <summary>
/// Stores physical properties for a node or group of nodes.
/// </summary>
public struct PropertySet
{
    /// <summary>
    /// Stores collider information for a PropertySet.
    /// </summary>
    public abstract class PropertySetCollider
    {
        /// <summary>
        /// Used for defining the type of collision for the PropertySetCollider.
        /// </summary>
        public enum PropertySetCollisionType : int
        {
            /// <summary>
            /// Used for approximating collision boundaries with a box.
            /// </summary>
            BOX = 0,

            /// <summary>
            /// Used for approximating collision boundaries with a sphere.
            /// </summary>
            SPHERE = 1,

            /// <summary>
            /// Used for taking the object's visible mesh and simplifying it for collision.
            /// </summary>
            MESH = 2
        }

        /// <summary>
        /// Stores the type of collision for the PropertySetCollider
        /// </summary>
        public PropertySetCollisionType CollisionType
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a new instance of the PropertySetCollider class.
        /// </summary>
        /// <param name="collisionType"></param>
        public PropertySetCollider(PropertySetCollisionType collisionType)
        {
            CollisionType = collisionType;
        }
    }

    public class BoxCollider : PropertySetCollider
    {
        /// <summary>
        /// The scale of the BoxCollider.
        /// </summary>
        public BXDVector3 Scale
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a new instance of the BoxCollider class.
        /// </summary>
        public BoxCollider(BXDVector3 scale)
            : base(PropertySetCollider.PropertySetCollisionType.BOX)
        {
            Scale = scale;
        }
    }

    public class SphereCollider : PropertySetCollider
    {
        /// <summary>
        /// The scale of the SphereCollider.
        /// </summary>
        public float Scale
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a new instance of the SphereCollider class.
        /// </summary>
        public SphereCollider(float scale)
            : base(PropertySetCollisionType.SPHERE)
        {
            Scale = scale;
        }
    }

    public class MeshCollider : PropertySetCollider
    {
        /// <summary>
        /// Determines whether or not the MeshCollider is convex.
        /// </summary>
        public bool Convex
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a new instance of the MeshCollider class.
        /// </summary>
        public MeshCollider(bool convex)
            : base(PropertySetCollisionType.MESH)
        {
            Convex = convex;
        }
    }

    /// <summary>
    /// A joint between two parts in a property set.
    /// Theoretically, this would joint to upper-most two parts in the heirarchy,
    /// merging all of the others into their parents.
    /// </summary>
    public class FieldJoint
    {
        /// <summary>
        /// The center point of the field joint.
        /// </summary>
        public BXDVector3 Center
        {
            get;
            private set;
        }
        /// <summary>
        /// The axis of rotation of the field joint.
        /// </summary>
        public BXDVector3 Axis
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a new instance of the FieldJoint class.
        /// </summary>
        public FieldJoint(BXDVector3 center, BXDVector3 axis)
        {
            Center = center;
            Axis = axis;
        }
    }

    /// <summary>
    /// ID of the PropertySet.
    /// </summary>
    public string PropertySetID;

    /// <summary>
    /// Collider of the PhysicsGroup.
    /// </summary>
    public PropertySetCollider Collider;

    /// <summary>
    /// Whether or not all members of the property set will exist as a single rigidbody in Unity.
    /// </summary>
    public bool Separated;

    /// <summary>
    /// Friction value of the PhysicsGroup.
    /// </summary>
    public int Friction;

    /// <summary>
    /// Stores the mass of the object.
    /// </summary>
    public float Mass;

    /// <summary>
    /// Stores the joint that exists between members of the object.
    /// </summary>
    public FieldJoint Joint;

    /// <summary>
    /// Constructs a new PhysicsGroup with the specified values.
    /// </summary>
    /// <param name="ID"></param>
    /// <param name="type"></param>
    /// <param name="frictionValue"></param>
    public PropertySet(string physicsGroupID, PropertySetCollider collider, bool separated, int friction, float mass = 0.0f, FieldJoint joint = null)
    {
        PropertySetID = physicsGroupID;
        Collider = collider;
        Separated = separated;
        Friction = friction;
        Mass = mass;
        Joint = joint;
    }
}