using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
namespace InGame.ObjectPool
{
    public class WeaponPoolManager : MonoBehaviour
    {
        #region 정적 멤버 및 싱글톤

        private static WeaponPoolManager s_instance;

        /// <summary>
        /// WeaponPoolManager의 전역 싱글톤 인스턴스입니다.
        /// </summary>
        public static WeaponPoolManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindAnyObjectByType<WeaponPoolManager>();
                    if (s_instance == null)
                    {
                        GameObject singletonObject = new GameObject(nameof(WeaponPoolManager));
                        s_instance = singletonObject.AddComponent<WeaponPoolManager>();
                        DontDestroyOnLoad(singletonObject);
                    }
                }
                return s_instance;
            }
        }

        #endregion

        #region 내부 상태 및 캐시

        /// <summary>각 타입별 오브젝트 풀을 저장하는 딕셔너리입니다.</summary>
        private Dictionary<Type, object> m_pools = new Dictionary<Type, object>();

        #endregion

        #region 풀 관리 및 제어

        /// <summary>
        /// 특정 타입의 오브젝트 풀을 가져오거나 새로 생성하여 등록합니다.
        /// </summary>
        /// <typeparam name="T">풀링할 오브젝트의 타입 (Component를 상속해야 함)</typeparam>
        /// <param name="createFunc">새 오브젝트를 생성하는 함수</param>
        /// <param name="actionOnGet">풀에서 오브젝트를 가져올 때 실행할 액션</param>
        /// <param name="actionOnRelease">오브젝트를 풀로 반환할 때 실행할 액션</param>
        /// <param name="actionOnDestroy">오브젝트 풀이 파괴될 때 실행할 액션</param>
        /// <param name="defaultCapacity">초기 풀 용량</param>
        /// <param name="maxSize">풀의 최대 크기</param>
        /// <returns>등록되거나 가져온 오브젝트 풀</returns>
        public IObjectPool<T> GetOrAddPool<T>(Func<T> createFunc, Action<T> actionOnGet, Action<T> actionOnRelease, Action<T> actionOnDestroy, int defaultCapacity = 10, int maxSize = 100) where T : Component
        {
            Type type = typeof(T);
            if (!m_pools.TryGetValue(type, out object poolObject))
            {
                IObjectPool<T> newPool = new ObjectPool<T>(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, true, defaultCapacity, maxSize);
                m_pools.Add(type, newPool);
                return newPool;
            }
            return (IObjectPool<T>)poolObject;
        }

        /// <summary>
        /// 특정 타입의 오브젝트를 풀에서 가져옵니다.
        /// </summary>
        /// <typeparam name="T">가져올 오브젝트의 타입</typeparam>
        /// <returns>풀에서 가져온 오브젝트</returns>
        public T Get<T>() where T : Component
        {
            Type type = typeof(T);
            if (m_pools.TryGetValue(type, out object poolObject))
            {
                IObjectPool<T> pool = (IObjectPool<T>)poolObject;
                return pool.Get();
            }
            Debug.LogError($"Pool for type {type.Name} not found. Please register the pool using GetOrAddPool first.");
            return null;
        }

        /// <summary>
        /// 특정 오브젝트를 풀로 반환합니다.
        /// </summary>
        /// <typeparam name="T">반환할 오브젝트의 타입</typeparam>
        /// <param name="item">반환할 오브젝트</param>
        public void Release<T>(T item) where T : Component
        {
            Type type = typeof(T);
            if (m_pools.TryGetValue(type, out object poolObject))
            {
                IObjectPool<T> pool = (IObjectPool<T>)poolObject;
                pool.Release(item);
            }
            else
            {
                Debug.LogWarning($"Pool for type {type.Name} not found. Cannot release item. Destroying GameObject instead.");
                // 풀이 없는 경우, 오브젝트를 파괴하여 메모리 누수 방지
                Destroy(item.gameObject);
            }
        }

        #endregion
    }
}
