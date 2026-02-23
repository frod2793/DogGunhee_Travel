using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace InGame.ObjectPool
{
    /// <summary>
    /// [설명]: 무기(투사체, 이펙트 등)를 타입별로 관리하는 중앙 풀링 매니저입니다.
    /// 공용 인터페이스를 통해 다양한 종류의 전투 오브젝트를 효율적으로 관리합니다.
    /// </summary>
    public class WeaponPoolManager : MonoBehaviour
    {
        #region 내부 필드

        /// <summary>
        /// [설명]: 타입(Type)을 키로 사용하여 각 컴포넌트별 오브젝트 풀을 저장합니다.
        /// </summary>
        private Dictionary<Type, object> m_pools = new Dictionary<Type, object>();

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 매니저 파괴 시 관리 중인 모든 풀 데이터를 정리합니다.
        /// </summary>
        private void OnDestroy()
        {
            if (m_pools != null)
            {
                m_pools.Clear();
                m_pools = null;
            }
        }

        #endregion

        #region 오브젝트 풀 관리

        /// <summary>
        /// [설명]: 특정 타입의 오브젝트 풀을 가져오거나, 없으면 새로 생성하여 등록합니다.
        /// </summary>
        /// <typeparam name="T">풀링할 오브젝트의 타입 (Component 상속)</typeparam>
        /// <param name="createFunc">새 오브젝트 생성 위임 함수</param>
        /// <param name="actionOnGet">풀에서 객체를 꺼낼 때 호출할 액션</param>
        /// <param name="actionOnRelease">풀에 객체를 반환할 때 호출할 액션</param>
        /// <param name="actionOnDestroy">풀이 정리되거나 한도 초과로 파괴 시 호출할 액션</param>
        /// <param name="defaultCapacity">초기 풀 용량</param>
        /// <param name="maxSize">최대 풀 용량</param>
        /// <returns>생성되거나 조회된 오브젝트 풀 인터페이스</returns>
        public IObjectPool<T> GetOrAddPool<T>(
            Func<T> createFunc,
            Action<T> actionOnGet,
            Action<T> actionOnRelease,
            Action<T> actionOnDestroy,
            int defaultCapacity = 10,
            int maxSize = 100) where T : Component
        {
            Type type = typeof(T);

            // 이미 존재하는 풀이 있다면 즉시 반환
            if (m_pools.TryGetValue(type, out object poolObject))
            {
                return (IObjectPool<T>)poolObject;
            }

            // 신규 풀 생성 및 딕셔너리에 등록
            IObjectPool<T> newPool = new ObjectPool<T>(
                createFunc,
                actionOnGet,
                actionOnRelease,
                actionOnDestroy,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );

            m_pools.Add(type, newPool);
            return newPool;
        }

        /// <summary>
        /// [설명]: 등록된 풀에서 해당 타입의 사용 가능한 오브젝트를 하나 가져옵니다.
        /// </summary>
        public T Get<T>() where T : Component
        {
            Type type = typeof(T);

            if (m_pools.TryGetValue(type, out object poolObject))
            {
                IObjectPool<T> pool = (IObjectPool<T>)poolObject;
                return pool.Get();
            }

            Debug.LogError($"[WeaponPoolManager] '{type.Name}' 타입의 풀이 등록되지 않았습니다. GetOrAddPool을 먼저 호출하세요.");
            return null;
        }

        /// <summary>
        /// [설명]: 사용이 끝난 무기 오브젝트를 해당 타입의 풀로 안전하게 반환합니다.
        /// </summary>
        public void Release<T>(T item) where T : Component
        {
            if (item == null)
            {
                return;
            }

            Type type = typeof(T);

            if (m_pools != null && m_pools.TryGetValue(type, out object poolObject))
            {
                IObjectPool<T> pool = (IObjectPool<T>)poolObject;
                pool.Release(item);
            }
            else
            {
                // 풀이 존재하지 않을 경우의 예외 처리 (강제 파괴)
                if (item != null && item.gameObject != null)
                {
                    Debug.LogWarning($"[WeaponPoolManager] '{type.Name}' 타입의 풀을 찾을 수 없어 오브젝트를 직접 파괴합니다.");
                    Destroy(item.gameObject);
                }
            }
        }

        /// <summary>
        /// [설명]: 특정 타입의 풀에 저장된 모든 대기 객체를 강제로 비웁니다.
        /// </summary>
        public void ClearPool<T>() where T : Component
        {
            Type type = typeof(T);
            if (m_pools.TryGetValue(type, out object poolObject))
            {
                IObjectPool<T> pool = (IObjectPool<T>)poolObject;
                pool.Clear();
            }
        }

        #endregion
    }
}