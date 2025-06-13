// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using Cysharp.Threading.Tasks;
//
// public struct Roi // 확인이 필요한 영역의 범위 구조체
// {
//     public int Left, Right, Bottom, Top;
//
//     public Roi(int left, int right, int bottom, int top) // 직사각형만 가능
//     {
//         Left = left;
//         Right = right;
//         Bottom = bottom;
//         Top = top;
//     }
//     
//     public Roi(Vector3Int center, Vector3Int offset) // 정사각형만 가능
//     {
//         Left = center.x - offset.x;
//         Right = center.x + offset.x;
//         Bottom = center.y - offset.y;
//         Top = center.y + offset.y;
//     }
// }
//
// public class Cell
// {
//     public bool Occupied = false;
//     
//     public HashSet<GameResourceNode> ResourceNodes { get; } = new HashSet<GameResourceNode>();
//     public HashSet<BuildingTypeHolder> Buildings { get; } = new HashSet<BuildingTypeHolder>();
//
//     //todo: 몬스터 추가
//     //todo: 아군 유닛 추가
//     public void Clear()
//     {
//         ResourceNodes.Clear();
//         Buildings.Clear();
//         //todo: 추가된 타입 Clear
//     }
//
//     public void ReleasePooledObjectInCell()
//     {
//         //todo: Cell에 포함된 IPoolObject 구현 클래스 (= PoolingManager로 오브젝트 풀링 적용한 클래스) 추가
//         foreach (var building in Buildings)
//         {
//             PoolingManager.Instance.ReleaseFromPool(building);
//         }
//     }
// }
//
// [RequireComponent(typeof(UnityEngine.Grid))]
// public class GridManager : Singleton<GridManager>
// {
//     private readonly Dictionary<Vector3Int, Cell> cells = new Dictionary<Vector3Int, Cell>();
//     [SerializeField] private UnityEngine.Grid grid; // serialize for debug
//
//     public const int CellSizeX = 1;
//     public const int CellSizeY = 1;
//     private int _unit;
//
//     public Grid Grid
//     {
//         get { return grid; }
//     }
//
//     enum Direction
//     {
//         Left,
//         Right,
//         Bottom,
//         Top,
//     }
//
//     protected override void Awake() 
//     {
//         base.Awake();
//         if (IsInvalidInstance()) return; // 중복 인스턴스인 경우 Init() 실행x
//         Init();
//     }
//
//     protected override void OnSceneLoaded(bool dummy)
//     {
//         Init();
//     }
//
//     private void Init()
//     {
//         FindGridMap();
//         
//         ResourceManager.Instance.SubscribePreLoad(InitAfterLoad);
//         GameSceneManager.Instance.RegisterCleanupTask(async () =>
//         {
//             await Clear();  
//         });
//     }
//
//     private void InitAfterLoad(bool dummy)
//     {
//         //todo : 리소스 로드 후 필요한 작업 추가
//     }
//
//     private void FindGridMap()
//     {
//         GameObject go = GameObject.FindWithTag("GridMap");
//         if (go == null)
//         {
//             Util.Log("Failed to find GridMap");
//             return;
//         }
//
//         grid = go.GetComponent<UnityEngine.Grid>();
//         // grid.cellSize = new Vector3(CellSizeX, CellSizeY, 0);
//         _unit = Mathf.Max(CellSizeX, CellSizeY);
//     }
//
//     #region Get Cell or Position(Range)
//
//     public Cell GetCell(Vector3Int cellPos)
//     {
//         if (cells.TryGetValue(cellPos, out Cell cell)) return cell;
//         cells[cellPos] = new Cell();
//
//         return cells[cellPos];
//     }
//     
//     public Cell GetCell(Vector2 cellPos) => GetCell(new Vector3Int((int)cellPos.x, (int)cellPos.y, 0));
//     public Vector3Int GetGridPosition(Vector3 pos) => grid.WorldToCell(pos);
//     public Vector3Int GetGridPosition(Component component) => grid.WorldToCell(component.transform.position);
//     public Vector3Int GetGridPosition(Transform obj) => grid.WorldToCell(obj.position);
//
//     private Roi GetRange(Vector3 pos, float range)
//     {
//         Vector3Int center = grid.WorldToCell(pos);
//         int offsetUnit = Mathf.CeilToInt(range / _unit);
//         Vector3Int offset = new Vector3Int(offsetUnit, offsetUnit, 0);
//         
//         return new Roi(center, offset);
//     }
//
//     private Vector3Int GetNearCellPos(int x, int y, Direction direction, int distance = 1) // 특정 방향으로 가장 가까운 셀 탐색
//     {
//         return direction switch
//         {
//             Direction.Left => new Vector3Int(x - (CellSizeX * distance), y, 0),
//             Direction.Right => new Vector3Int(x + (CellSizeX * distance), y, 0),
//             Direction.Bottom => new Vector3Int(x, y - (CellSizeY * distance), 0),
//             Direction.Top => new Vector3Int(x, y + (CellSizeY * distance), 0),
//             _ => Vector3Int.one
//         };
//     }
//
//     #endregion
//
//     #region Gather Objects from Cells
//
//     public void GatherResourceNodes(Vector3 pos, float range, Action<List<GameResourceNode>> actions)
//     {
//         var nodes = GatherResourceNodes(pos, range);
//         try
//         {
//             actions?.Invoke(nodes);
//         }
//         finally
//         {
//             UnityEngine.Pool.ListPool<GameResourceNode>.Release(nodes);
//         }
//     }
//     
//     private List<GameResourceNode> GatherResourceNodes(Vector3 pos, float range)
//     { // 해당 함수를 사용한 후, 반드시 반환된 리스트를 ListPool<GamResourceNode>.Release()로 반환할 것
//         var nodes = UnityEngine.Pool.ListPool<GameResourceNode>.Get();
//         Roi roi = GetRange(pos, range);
//
//         for (int x = roi.Left; x <= roi.Right; x++)
//         {
//             for (int y = roi.Bottom; y <= roi.Top; y++)
//             {
//                 if (cells.TryGetValue(new Vector3Int(x, y, 0), out Cell cell) && cell.ResourceNodes.Count > 0)
//                     nodes.AddRange(cell.ResourceNodes);
//             }
//         }
//         
//         return nodes;
//     }
//
//     public GameResourceNode GetNearestNode(Vector3 pos, int range)
//     {
//         List<GameResourceNode> nodes = GatherResourceNodes(pos, range);
//         if (nodes == null || nodes.Count == 0) return null;
//
//         GameResourceNode target = null;
//         float minDist = float.MaxValue;
//
//         for (int i = 0; i < nodes.Count; i++)
//         {
//             float sqrDist = (pos - nodes[i].transform.position).sqrMagnitude;
//             if (sqrDist < minDist)
//             {
//                 minDist = sqrDist;
//                 target = nodes[i];
//             }
//         }
//
//         UnityEngine.Pool.ListPool<GameResourceNode>.Release(nodes);
//         return target;
//     }
//
//     #endregion
//
//     #region Add Object To Grid
//
//     public async UniTask<Cell> AddToGrid<T>(T component, bool delayFrame) where T : Component
//     {
//         if (delayFrame)
//             return await AddToGridAsync(component);
//         
//         return AddToGridImmediate(component);
//     }
//     private async UniTask<Cell> AddToGridAsync<T>(T component) where T : Component
//     {
//         await UniTask.Yield();
//         return AddToGridImmediate(component);
//     }
//     public void AddToGridVoid<T>(T component, bool delayFrame = true) where T : Component
//     {
//         if (delayFrame)
//         {
//             UniTask.Void(async () =>
//             {
//                 await UniTask.Yield();
//                 AddToGridVoidImmediate(component);
//             });
//         }
//         else
//         {
//             AddToGridVoidImmediate(component);
//         }
//     }
//     
//     private Cell AddToGridImmediate<T>(T component) where T : Component
//     {
//         Cell cell = GetCell(GetGridPosition(component));
//         AddComponentToCell(component, cell);
//         return cell;
//     }
//
//     private void AddToGridVoidImmediate<T>(T component) where T : Component
//     {
//         Cell cell = GetCell(GetGridPosition(component));
//         AddComponentToCell(component, cell);
//     }
//     
//     private static void AddComponentToCell<T>(T component, Cell cell) where T : Component
//     {
//         switch (component)
//         {
//             case GameResourceNode node:
//                 cell.ResourceNodes.Add(node);
//                 break;
//             case BuildingTypeHolder building:
//                 cell.Buildings.Add(building);
//                 break;
//             default:
//                 Util.Log($"{nameof(GridManager)}: Invalid component type for grid");
//                 break;
//         }
//     }
//
//     #endregion
//     
//     #region Remove Object from Grid
//
//     public void RemoveFromGrid<T>(T component) where T : Component
//     {
//         Cell cell = GetCell(GetGridPosition(component));
//         switch (component)
//         {
//             case GameResourceNode node:
//                 cell.ResourceNodes.Remove(node);
//                 break;
//             case BuildingTypeHolder building:
//                 cell.Buildings.Remove(building);
//                 break;
//             default:
//                 Util.Log($"{nameof(GridManager)}.RemoveFromGrid: Invalid component type for grid");
//                 break;
//         }
//     }
//
//     public bool RemoveFromGrid<T>(Cell cell, T component) where T : Component
//     { // 소속 셀을 아는 경우 사용. cell 안에 component가 없으면 false 반환
//         return component switch
//         {
//             GameResourceNode node => cell.ResourceNodes.Remove(node),
//             BuildingTypeHolder building => cell.Buildings.Remove(building),
//             _ => false
//         };
//     }
//
//     #endregion
//     
//
//     private void CleanCell(Cell cell)
//     {
//         cell.Clear();
//     }
//
//     private UniTask Clear()
//     {
//         foreach (var cell in cells.Values)
//         {
//             cell.ReleasePooledObjectInCell();
//             cell.Clear();
//         }
//
//         return UniTask.CompletedTask;
//     }
// }
