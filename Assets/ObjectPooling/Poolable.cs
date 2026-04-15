using UnityEngine;
using System;

namespace ToolBox.Pools
{
	[DisallowMultipleComponent]
	internal class Poolable : MonoBehaviour
	{
		private IPoolable[] _poolables = Array.Empty<IPoolable>();
		private bool _isInitialized;

		private void Awake()
		{
			_poolables = GetComponentsInChildren<IPoolable>(true);
			_isInitialized = true;
		}

		private void OnDestroy()
		{
			Pool.Remove(gameObject);
		}

		public void OnPool()
		{
			if (!_isInitialized)
				return;

			for (var i = 0; i < _poolables.Length; i++)
				_poolables[i].OnPool();
		}

		public void OnDepool()
		{
			for (var i = 0; i < _poolables.Length; i++)
				_poolables[i].OnDepool();
		}
	}
}
