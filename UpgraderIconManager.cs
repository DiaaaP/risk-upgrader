using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RiskUpgrader
{
    internal sealed class UpgraderIconManager : IDisposable
    {
        private readonly Dictionary<string, AsyncOperationHandle<Sprite>> loads = new Dictionary<string, AsyncOperationHandle<Sprite>>();
        private readonly Dictionary<string, Sprite> resolved = new Dictionary<string, Sprite>();
        private readonly HashSet<string> failed = new HashSet<string>();

        public Sprite Get(ItemDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.name)) return null;
            Sprite sprite;
            if (resolved.TryGetValue(def.name, out sprite) && sprite != null) return sprite;
            try
            {
                PickupDef pickup = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(def.itemIndex));
                sprite = pickup == null ? null : pickup.GetIcon();
                if (sprite != null)
                {
                    resolved[def.name] = sprite;
                    return sprite;
                }
                if (failed.Contains(def.name)) return null;
                AsyncOperationHandle<Sprite> handle;
                if (loads.TryGetValue(def.name, out handle))
                {
                    if (!handle.IsValid() || !handle.IsDone) return null;
                    if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                    {
                        resolved[def.name] = handle.Result;
                        return handle.Result;
                    }
                    failed.Add(def.name);
                    return null;
                }
                var reference = def.GetIconReference();
                if (reference == null || !reference.RuntimeKeyIsValid()) return null;
                loads[def.name] = Addressables.LoadAssetAsync<Sprite>(reference.RuntimeKey);
                return null;
            }
            catch
            {
                failed.Add(def.name);
                return null;
            }
        }

        public void Dispose()
        {
            foreach (AsyncOperationHandle<Sprite> handle in loads.Values)
                if (handle.IsValid()) Addressables.Release(handle);
            loads.Clear();
            resolved.Clear();
            failed.Clear();
        }
    }
}
