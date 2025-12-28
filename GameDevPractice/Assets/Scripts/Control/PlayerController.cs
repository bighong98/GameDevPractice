using UnityEngine;
using TH.Attribute;
using UnityEngine.AI;
using TH.Utils;
using System;
using TH.Control.Movement;
using TH.Core;
using TH.Core.Service;

namespace TH.Control
{
    public interface IPlayerController
    {
        ComponentProvider Components { get; }
    }
    
    public class PlayerController : MonoBehaviour, IPlayerController
    {
        [SerializeField] private Camera _camera;
        // private Mover mover;
        private MoverRefactoring mover;
        private IFighter fighter;
        private Health health;

        public ComponentProvider Components { get; private set; }

        private Vector2 wasdInput = Vector2.zero;
        private bool isWASDMoving = false;
        private bool fightEnabled = true;
        private const float MaxNavMeshProjectionDistance = 1f;

        private void Awake()
        {
            Components = new ComponentProvider(gameObject);
            
            TryGetComponent(out mover);
            TryGetComponent(out fighter);
            TryGetComponent(out health);

            ServiceLocator.Get<IPlayerHolder>().SetPlayer(this);
        }

        private void OnEnable()
        {
            InputManager.Instance.OnSelected += OnPointerPressed;
            InputManager.Instance.OnMoved += OnWASDInput;
        }

        private void OnDisable()
        {
            InputManager.Instance.OnSelected -= OnPointerPressed;
            InputManager.Instance.OnMoved -= OnWASDInput;
        }

        private void Start()
        {
            _camera = Camera.main;
        }

        private void Update()
        {
            if (Time.timeScale <= float.Epsilon || health?.IsDead == true)
            {
                return;
            }

            // WASD 입력이 있으면 지속적으로 이동 처리
            if (isWASDMoving && wasdInput.sqrMagnitude > 0.01f)
            {
                HandleWASDMovement();
            }
        }

        private void OnWASDInput(Vector2 input)
        {
            wasdInput = input;
            isWASDMoving = input.sqrMagnitude > 0.01f;
            
            // WASD 입력이 시작되면 즉시 기존 이동/전투 취소
            if (isWASDMoving)
            {
                mover.CancelAction();
            }
        }

        private void HandleWASDMovement()
        {
            // 카메라 방향 기준으로 입력 변환
            Vector3 cameraForward = _camera.transform.forward;
            Vector3 cameraRight = _camera.transform.right;
            
            // Y축 제거 (수평 이동만)
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            // 이동 방향 계산
            Vector3 moveDirection = (cameraForward * wasdInput.y + cameraRight * wasdInput.x).normalized;
            
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                // 현재 위치에서 이동 방향으로 목표 지점 설정 (더 짧은 거리)
                Vector3 targetPosition = transform.position + moveDirection * 2f; // 10f -> 2f로 변경
                
                // NavMesh 위의 유효한 위치로 변환
                if (NavMesh.SamplePosition(targetPosition, out NavMeshHit navMeshHit, MaxNavMeshProjectionDistance, NavMesh.AllAreas))
                {
                    mover.Moveto(navMeshHit.position);
                }
            }
        }


        private void OnPointerPressed(Vector2 pos)
        {
            Logg.Log($"[{nameof(PlayerController)}.{nameof(OnPointerPressed)}()] triggered", Logg.LoggingMode.Completed);
            
            if (Time.timeScale <= float.Epsilon || health?.IsDead == true)
            {
                return; // 게임이 일시정지 중이거나 플레이어가 사망한 경우 반응 없음
            }
            
            // WASD 이동 중이면 포인터 입력 무시 (WASD 우선)
            if (isWASDMoving)
            {
                return;
            }
            
            if (fightEnabled && TryInteractWithComponent(pos))
            {
                return; // 우선순위: 전투 > 이동
            }
            
            if (TryMoveTo(pos))
            {
                return;
            }
            
            SetCursor(CursorType.None);
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
            if (!RaycastWithNavMesh(pos, out var navMeshPos)) return false;
            
            SetCursor(CursorType.Movement);
            // mover.StartMoveAction(navMeshPos);
            return true;
        }
        
        private bool RaycastWithNavMesh(Vector2 pointerPos, out Vector3 target)
        {
            if (Physics.Raycast(GetPointerRay(pointerPos), out RaycastHit hit) &&
                NavMesh.SamplePosition(hit.point, out NavMeshHit navMeshHit, MaxNavMeshProjectionDistance, NavMesh.AllAreas))
            {
                target = navMeshHit.position;
                return true;
            }

            target = Vector3.zero;
            return false;
        }
        
        private Ray GetPointerRay(Vector2 pos)
        {
            return _camera.ScreenPointToRay(pos);
        }

        private void SetCursor(CursorType cursor)
        {
            
        }
    }
}
