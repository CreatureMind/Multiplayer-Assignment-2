/*using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public struct MyNetworkInput : INetworkInput
{
    public Vector3 MovementDirection;
    public NetworkBool JumpButton;
    public NetworkBool FireButton;
}

namespace Fusion.Sample.DedicatedServer {

    public class PlayerController : NetworkBehaviour {
    
        [SerializeField] private float Speed = 20f;

        private NetworkRigidbody3D _nrb;

        public override void Spawned() {
            _nrb = GetComponent<NetworkRigidbody3D>();
        }

        public override void FixedUpdateNetwork() {
            Vector3 direction = default;

            // Extract your type-safe input struct instead of the legacy prototype
            if (GetInput(out MyNetworkInput input)) {
                // Assign the pre-calculated direction vector provided by the client
                direction = input.MovementDirection;
            }

            // Move player using the NetworkRigidbody Component
            if (_nrb && !_nrb.Rigidbody.isKinematic) {
                _nrb.Rigidbody.AddForce(direction * Speed);
            }
        }
    }
}*/