using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 화면 내에서 계속 튕기는 단 하나의 영구적인 진주를 발사하는 무기입니다.
    /// 진주는 무기가 교체되기 전까지 사라지지 않으며, 공격 키를 다시 눌러도 추가로 발사되지 않습니다.
    /// 특정 장신구 장착 시, 적에게 스턴 효과를 부여합니다.
    /// </summary>
    public class WeaphonJinjoo : Weaphon_base
    {
        [Header("진주 공격 설정")]
        [Tooltip("발사할 진주 프리팹입니다. PearlProjectile 스크립트가 있어야 합니다.")]
        [SerializeField] private GameObject pearlPrefab;

        [Header("업그레이드 설정 (장신구)")]
        
        private GameObject _activePearlInstance; // 현재 활성화된 진주 인스턴스

        public override void OnEnable()
        {
            base.OnEnable();
            // 무기가 활성화될 때, 기존에 남아있을 수 있는 진주가 없도록 초기화합니다.
            _activePearlInstance = null;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            // 무기가 비활성화(교체)될 때, 활성화된 진주가 있다면 풀에 반환합니다.
            if (_activePearlInstance != null && _activePearlInstance.activeInHierarchy)
            {
                var objectPooler = VamserLikeGameManager.Instance?.objectPoolSpawner;
                if (objectPooler != null)
                {
                    objectPooler.ReturnObject(_activePearlInstance);
                }
            }
            _activePearlInstance = null;
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            base.Weaphon_Attack(attackAngle);

            // 이미 활성화된 진주가 없다면 새로 발사합니다.
            if (_activePearlInstance == null || !_activePearlInstance.activeInHierarchy)
            {
                LaunchPearl();
            }
        }

        private void LaunchPearl()
        {
            var objectPooler = VamserLikeGameManager.Instance.objectPoolSpawner;
            if (objectPooler == null || pearlPrefab == null)
            {
                Debug.LogError("ObjectPooler 또는 PearlPrefab이 할당되지 않았습니다.");
                return;
            }

            // 오브젝트 풀에서 진주 스폰
            _activePearlInstance = objectPooler.SpawnObject(pearlPrefab, transform.position, Quaternion.identity);
            if (_activePearlInstance == null)
            {
                return;
            }

            // 진주 초기화
            var pearlProjectile = _activePearlInstance.GetComponent<PearlProjectile>();
            if (pearlProjectile != null)
            {
                // 진주에 필요한 모든 데이터를 전달하여 초기화합니다. (결합도 감소)
                // 이제 lifetime은 외부에서 관리하므로 전달하지 않습니다.
                pearlProjectile.Initialize(attackSpeed, attackPower, isUpgradelv2, mobStunTime);
            }
            else
            {
                Debug.LogError("PearlPrefab에 PearlProjectile 컴포넌트가 없습니다.");
                objectPooler.ReturnObject(_activePearlInstance);
                _activePearlInstance = null;
            }
        }
    }
}
