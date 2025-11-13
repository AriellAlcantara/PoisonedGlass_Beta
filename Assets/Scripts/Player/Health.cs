using Fusion;
using UnityEngine;

namespace GNW2.Player
{
    public class Health : NetworkBehaviour
    {
        private int maxHealth = 60;
        [Networked] private int _currentHealth { get; set; }
        [SerializeField] private ParticleSystem _hitFx;

        private Player _currentPlayer;


        private void Start()
        {
            _currentHealth = maxHealth;
            _currentPlayer = GetComponent<Player>();
            _currentPlayer.OnTakeDamage += TakeHealthDamage;
        }

        public override void FixedUpdateNetwork()
        {
        }

        private void TakeHealthDamage(int damage)
        {
            _currentHealth -= damage;
            Debug.Log($"Current Health: {_currentHealth}");
            RPC_SpawnHitFx(transform.position);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SpawnHitFx(Vector3 position)
        {

            if (_hitFx != null)
            {
                Instantiate(_hitFx, position, Quaternion.identity);
            }
        }




    }
}
