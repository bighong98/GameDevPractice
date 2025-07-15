using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG.Attribute;
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
        
        private bool fightEnabled = true;

        private void Start()
        {
            mover = GetComponent<Mover>();
            fighter = GetComponent<Fighter>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _camera = Camera.main;
            InputManager.Instance.OnSelected += OnPointerPressed;
        }

        private void OnDisable()
        {
            if (Util.IsQuitting) return;
            _camera = null;
            InputManager.Instance.OnSelected -= OnPointerPressed;
        }

        private void OnPointerPressed(Vector2 pos)
        {
            if (Time.timeScale <= float.Epsilon || health is {IsDead: true} ) return; // 게임이 일시정지 중인 경우 반응x //todo: 게임 일시정지 여부 확인 로직 수정
            if (fightEnabled && TryCombat(pos)) return; // 우선순위: 전투 > 이동
            TryMoveTo(pos);
        }

        private const int MaxRaycastHitNum = 100;
        private readonly RaycastHit[] lastHits = new RaycastHit[MaxRaycastHitNum];
        private bool TryCombat(Vector2 pos)
        {
            if (Physics.RaycastNonAlloc(GetPointerRay(pos), lastHits) is int hitLength and > 0)
            {
                for (int i = 0 ; i < hitLength; i++)
                {
                    if (!fighter.CanAttack(lastHits[i].transform.gameObject, out Health targetHealth)) continue;
                    
                    fighter.Attack(targetHealth);
                    return true;
                }
            }

            return false;
        }

        private bool TryMoveTo(Vector2 pos)
        {
            if (!Physics.Raycast(GetPointerRay(pos), out var hit)) return false;
            
            mover.StartMoveAction(hit.point);
            return true;
        }
        
        private Ray GetPointerRay(Vector2 pos)
        {
            return _camera.ScreenPointToRay(pos);
        }

        #region Deprecated

        // private bool InteractWithCombat()
        // {
        //     if (Physics.RaycastNonAlloc(GetMouseRay, lastHits) is int hitLength and > 0) // 여러명일 때는 전부 때릴 것 같은데?
        //     {
        //         for (int i = 0 ; i < hitLength; i++)
        //         {
        //             GameObject target = lastHits[i].transform.gameObject;
        //             
        //             if (!fighter.CanAttack(target, out Health targetHealth)) continue; // 현재 fighter.Attack과 함께 두번 GetComponent를 실행하고 있음     
        //             
        //             if (Input.GetMouseButtonDown(0))
        //                 fighter.Attack(targetHealth);
        //             
        //             return true;
        //         }
        //     }
        //
        //     return false;
        // }
        // private bool InteractWithMovement()
        // {
        //     if (Physics.Raycast(GetMouseRay, out var hit))
        //     {
        //         if (Input.GetMouseButton(0))
        //             mover.StartMoveAction(hit.point);
        //         return true;
        //     }
        //     
        //     return false;
        // }
        // private Ray GetMouseRay => _camera.ScreenPointToRay(Input.mousePosition);

        #endregion
    }
}
