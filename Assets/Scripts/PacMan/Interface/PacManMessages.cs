using System.Collections.Generic;
using UnityEngine;

namespace PacMan
{
    using System.Collections.Generic;
    using Unity.Netcode;
    using UnityEngine;

    namespace PacMan
    {
        public struct PacManAction : INetworkSerializable
        {
            public Vector2 AccelerationDirection;
            public float AccelerationMagnitude;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref AccelerationDirection);
                serializer.SerializeValue(ref AccelerationMagnitude);
            }
        }

        public struct PacManObservations : INetworkSerializable
        {
            public PacManObservation[] Observations;
            public int Index;
            public float ObservationFixedTime;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Observations);
                serializer.SerializeValue(ref Index);
                serializer.SerializeValue(ref ObservationFixedTime);
            }
        }

        public struct PacManObservation : INetworkSerializable
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public bool IsGhost;
            public bool Visible;
            public float ReadingDispersion;
            public bool HasFood;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Velocity);
                serializer.SerializeValue(ref Visible);
                serializer.SerializeValue(ref ReadingDispersion);
                serializer.SerializeValue(ref IsGhost);
                serializer.SerializeValue(ref HasFood);
            }
        }
    }
}