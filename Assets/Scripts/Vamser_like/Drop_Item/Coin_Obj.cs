using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class Coin_Obj : MonoBehaviour,IObjectPoolSpawnerSettable 
    {
        public ObjectPoolSpawner objectPoolSpawner { get; set; }

        private void OnEnable()
        {
            // 코인 오브젝트가 활성화될 때 필요한 초기화 작업을 수행할 수 있습니다.
        }

        private void OnDisable()
        {
            // 코인 오브젝트가 비활성화될 때 필요한 정리 작업을 수행할 수 있습니다.
        }
    }
}