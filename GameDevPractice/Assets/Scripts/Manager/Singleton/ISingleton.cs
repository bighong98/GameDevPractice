using UnityEngine;

public interface ISingleton
{
    void OnSceneLoaded(bool isDone); // 씬 이동/재시작마다 초기화하는 참조를 필요로 하는 작업을 수행
}
