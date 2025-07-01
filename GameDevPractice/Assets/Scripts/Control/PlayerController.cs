using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG.Core;
using RPG.Movement;
using RPG.Combat;

namespace RPG.Control
{
    public class PlayerController : MonoBehaviour
    {
        private Camera _camera;
        private Mover mover;
        private Fighter fighter;
        private Health health;

        private void Start()
        {
            _camera = Camera.main;
            mover = GetComponent<Mover>();
            fighter = GetComponent<Fighter>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (health.IsDead) return;
            if (InteractWithCombat()) return; // 우선순위: 전투 > 이동
            if (InteractWithMovement()) return;
            
            // print("Nothing to do. mouse is out of world");
        }
        
        private bool InteractWithMovement()
        {
            if (Physics.Raycast(GetMouseRay, out var hit))
            {
                if (Input.GetMouseButton(0))
                    mover.StartMoveAction(hit.point);
                return true;
            }
                
            return false;
        }

        private readonly RaycastHit[] lastHits = new RaycastHit[100]; // 100 is magic number
        private bool InteractWithCombat()
        {
            if (Physics.RaycastNonAlloc(GetMouseRay, lastHits) is int hitLength and > 0) // 여러명일 때는 전부 때릴 것 같은데?
            {
                for (int i = 0 ; i < hitLength; i++)
                {
                    GameObject target = lastHits[i].transform.gameObject;
                    
                    if (!fighter.CanAttack(target, out Health targetHealth)) continue; // 현재 fighter.Attack과 함께 두번 GetComponent를 실행하고 있음     
                    
                    if (Input.GetMouseButtonDown(0))
                        fighter.Attack(targetHealth);
                    
                    return true;
                }
            }

            return false;
        }

        private Ray GetMouseRay => _camera.ScreenPointToRay(Input.mousePosition);
    }
}
