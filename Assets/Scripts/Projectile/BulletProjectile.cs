using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using GNW2.Player;
using UnityEngine;

namespace GNW2.Projectile
{
    public class BulletProjectile : NetworkBehaviour
    {
        [SerializeField] private float bulletSpeed = 10f;
        [SerializeField] private float lifeTime = 5f;
        [SerializeField] private int damage  = 1;
        
        [Networked]private TickTimer life { get; set; }

        public void Init()
        {
            life = TickTimer.CreateFromSeconds(Runner, lifeTime);
        }

        public override void FixedUpdateNetwork()
        {
            if (life.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
            else
            {
                transform.position += bulletSpeed * transform.forward * Runner.DeltaTime;
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            Debug.Log($"Hit Collider {other.collider.name}");
            if (Object.HasStateAuthority)
            {
                
                var combatInterface = other.collider.GetComponent<ICombat>();
                if (combatInterface != null)
                {
                    combatInterface.TakeDamage(damage);
                }
                else
                {
                    Debug.LogError("Combat Interface Found");
                }
                
                Runner.Despawn(Object);
            }
        }
        
        

        

    }
}
