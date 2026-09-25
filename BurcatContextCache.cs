using BurcatProtocol.Cache;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace BurcatProtocol
{
    public static class BurcatContextCache
    {
        private static bool IsLogicInCache { get; set; } = false;
        private static object CacheLock { get; } = new();

        private static ConcurrentDictionary<MethodKey, bool> NeedsContext { get; } = [];

        public static void AddLogicIntoCache()
        {
            if (!IsLogicInCache)
                lock (CacheLock)
                    if (!IsLogicInCache)
                    {
                        BurcatCache.MethodAddition += BurcatCache_MethodAddition;
                        BurcatCache.BeforeExecutingAction += BurcatCache_BeforeExecutingAction;

                        IsLogicInCache = true;
                    }
        }

        private static void BurcatCache_MethodAddition(object? sender, MethodAdditionEventArgs e)
        {
            bool needsContext = e.Method.GetCustomAttribute<BurcatContextAttribute>() is not null;
            NeedsContext.AddOrUpdate(new(GuidList.FromType(e.ObjectType), e.Method.Name), needsContext, (k, o) => o || needsContext );
        }

        public static AsyncLocal<BurcatContext?> Context { get; } = new();

        private static void BurcatCache_BeforeExecutingAction(object? sender, BeforeExecutingActionEventArgs e)
        {
            if (NeedsContext.TryGetValue(new(GuidList.FromType(e.ObjectType), e.TargetAction), out bool needsHead) && needsHead)
                if (Context.Value is BurcatContext context) e.Parameters = [context, .. e.Parameters];
                else throw new NullReferenceException("No context was provided and the method needs context.");
        }

        private class MethodKey(GuidList classGuid, string name)
        {
            public GuidList ClassGuid { get; } = classGuid;
            public string Name { get; } = name.ToLower().Replace("_", null);

            public override bool Equals(object? obj)
            {
                if (obj is null) return false;
                else if (ReferenceEquals(this, obj)) return true;
                else if (obj is MethodKey other) return ClassGuid.Equals(other.ClassGuid) && Name.Equals(other.Name);
                else return false;
            }
            public override int GetHashCode() => HashCode.Combine(ClassGuid, Name);
        }
    }
}
