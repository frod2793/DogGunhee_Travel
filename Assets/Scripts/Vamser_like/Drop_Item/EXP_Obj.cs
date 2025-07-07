using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class EXP_Obj : MonoBehaviour ,IObjectPoolSpawnerSettable 
    {
        public float expValue = 10f; // 경험치 값

        // private void OnTriggerEnter(Collider other)
        // {
        //     if (other.CompareTag("Player"))
        //     {
        //         PlayerBase player = other.GetComponent<PlayerBase>();
        //         if (player != null)
        //         {
        //             player.AddExperience(expValue); // 플레이어에게 경험치 추가
        //             objectPoolSpawner?.ReturnToPool(this); // 오브젝트 풀로 반환
        //         }
        //     }
        // }

        public ObjectPoolSpawner objectPoolSpawner { get; set; }
    }
}