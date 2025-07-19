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

        private void Awake()
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
            // if (Util.IsQuitting) return;
            _camera = null;
            InputManager.Instance.OnSelected -= OnPointerPressed;
        }

        private void OnPointerPressed(Vector2 pos)
        {
            if (Time.timeScale <= float.Epsilon || health is {IsDead: true} ) return; // 게임이 일시정지 중인 경우 반응x //todo: 게임 일시정지 여부 확인 로직 수정
            // if (fightEnabled && TryCombat(pos)) return; // 우선순위: 전투 > 이동
            if (fightEnabled && TryInteractWithComponent(pos)) return; // 우선순위: 전투 > 이동
            if (TryMoveTo(pos)) return;
            
            SetCursor(CursorType.None); // 현재 커서 관련 로직은 강의 영상과 다르게 작동함 (강의: Update() 실행 + 마우스 포인터가 움직일 때마다 갱신, 현재 코드: InputSystem 콜백 기반 실행 + 마우스 클릭마다 갱신)
        }

        private const int MaxRaycastHitNum = 100;
        private readonly RaycastHit[] hitResults = new RaycastHit[MaxRaycastHitNum];

        private bool TryInteractWithComponent(Vector2 pointerPos)
        {
            if (Physics.RaycastNonAlloc(GetPointerRay(pointerPos), hitResults) is not (int hitLength and > 0))
                return false;
            
            for (int i = 0; i < hitLength; i++) // todo: 거리순서로 정렬된 배열을 사용하는 것을 고려
            {
                if (hitResults[i].transform.GetComponents<IRaycastable>() is not { } raycastables) continue;
                
                foreach (var raycastable in raycastables)
                {
                    if (!raycastable.HandleRaycast(this)) continue;
                    
                    SetCursor(raycastable.GetCursorType());
                    return true;
                }
            }
            
            return false;
        }
        
        
        private bool TryCombat(Vector2 pos)
        {
            if (Physics.RaycastNonAlloc(GetPointerRay(pos), hitResults) is int hitLength and > 0)
            {
                for (int i = 0 ; i < hitLength; i++)
                {
                    if (!fighter.CanAttack(hitResults[i].transform.gameObject, out Health targetHealth)) continue;
                    
                    SetCursor(CursorType.Combat);
                    fighter.Attack(targetHealth);
                    return true;
                }
            }

            return false;
        }

        private bool TryMoveTo(Vector2 pos)
        {
            if (!Physics.Raycast(GetPointerRay(pos), out var hit)) return false;
            
            SetCursor(CursorType.Movement);
            mover.StartMoveAction(hit.point);
            return true;
        }
        
        private Ray GetPointerRay(Vector2 pos)
        {
            return _camera.ScreenPointToRay(pos);
        }

        private void SetCursor(CursorType cursor)
        {
            
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
