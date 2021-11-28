using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System;

namespace FieldsObserver
{

    public class FieldsObserver<T>
    {
        private class ObserverGroup
        {
            public List<FieldInfo> Fields { get; set; } = new List<FieldInfo>();

            public List<Action> MethodsToInove { get; set; } = new List<Action>();

            public bool Changed { get; set; } = false;

            public bool ContainsField(FieldInfo field) => Fields.Contains(field);
        }

        private class HistoryObject
        {
            public object LastValue { get; set; }

            public void Update(object actualValue)
            {
                LastValue = actualValue.Copy(); // TODO shallow or deep copy
            }

            public HistoryObject(object value)
            {
                IUpdatable[] enumerable = value as IUpdatable[];

                if (enumerable != null)
                {
                    var tmp1 = new List<IUpdatable>(enumerable.Length);

                    for (int i = 0; i < enumerable.Length; i++)
                    {
                        tmp1.Add(new BaseClass());
                    }

                    LastValue = tmp1.ToArray();

                    for (int i = 0; i < enumerable.Length; i++)
                    {
                        var instance = Activator.CreateInstance(enumerable[0].GetType()) as IUpdatable;
                        instance.Update(enumerable[i]);
                        (LastValue as IUpdatable[])[i] = instance;
                    }
                }
                else
                {
                    LastValue = value.Copy();
                }
            }
        }

        private readonly Dictionary<string, ObserverGroup> _groups = new Dictionary<string, ObserverGroup>() { };
        private readonly Dictionary<string, HistoryObject> _fieldsValueHistory = new Dictionary<string, HistoryObject>();
        private readonly List<FieldInfo> _allfieldsToUpdate;
        private readonly object _classToUpdate;

        public FieldsObserver(object classToUpdate)
        {
            _classToUpdate = classToUpdate;
            _groups.Add("default", new ObserverGroup());

            _allfieldsToUpdate = typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                          .Where(field => field.GetCustomAttributes(typeof(ObserveField), false).Length > 0)
                                          .ToList();

            _allfieldsToUpdate.ForEach(field =>
            {
                var groupNames = field.GetCustomAttribute<ObserveField>().GroupNames;
                foreach (var groupName in groupNames)
                {
                    if (!_groups.ContainsKey(groupName))
                    {
                        _groups[groupName] = new ObserverGroup();
                    }

                    _groups[groupName].Fields.Add(field);
                }
            });

            typeof(T).GetMethods().ToList().ForEach(method =>
            {
                var groupNames = method.GetCustomAttribute<InvokeOnChange>()?.GroupNames;

                if (groupNames != null && groupNames.Length > 0)
                {
                    foreach (var groupName in groupNames)
                    {
                        if (!_groups.ContainsKey(groupName))
                        {
                            _groups[groupName] = new ObserverGroup();
                        }
                        _groups[groupName].MethodsToInove.Add(Delegate.CreateDelegate(typeof(Action), classToUpdate, method) as Action);
                    }
                }
            });

            foreach (var field in _allfieldsToUpdate)
            {
                //Adding starting values to history
                _fieldsValueHistory.Add(field.Name, new HistoryObject(field.GetValue(_classToUpdate)));
            }
        }

        public void CheckForUpdates()
        {
            foreach (var field in _allfieldsToUpdate)
            {
                _fieldsValueHistory.TryGetValue(field.Name, out var historyValue);
                var actualFieldValue = field.GetValue(_classToUpdate);//always return new referance

                var type = historyValue.LastValue.GetType();
                var castedLastValue = Helper.Cast(historyValue.LastValue, type);
                if (Helper.IsIEnumerableOfT(type))
                {
                    bool changed = false;
                    for (int i = 0; i < castedLastValue.Length; i++)
                    {
                        if (!castedLastValue[i].Equals((actualFieldValue as object[])[i]))
                        {
                            changed = true;
                            break;
                        }
                    }

                    if (changed)
                    {
                        for (int i = 0; i < castedLastValue.Length; i++)
                        {
                            castedLastValue[i].Update((actualFieldValue as object[])[i]);
                        }

                        CheckIfGroupHasField(field);
                    }
                }
                else if (!castedLastValue.Equals(actualFieldValue))
                {
                    historyValue.Update(actualFieldValue);
                    CheckIfGroupHasField(field);

                }
            }

            foreach (var group in _groups)
            {
                if (group.Value.Changed)
                {
                    group.Value.MethodsToInove.ForEach(method => method.Invoke());
                }

                group.Value.Changed = false;
            }
        }

        private void CheckIfGroupHasField(FieldInfo field)
        {
            foreach (var group in _groups)
            {
                group.Value.Changed = group.Value.ContainsField(field);
            }
        }
    }

    public static class Helper
    {
        public static dynamic Cast(dynamic obj, Type castTo)
        {
            return Convert.ChangeType(obj, castTo);
        }

        public static bool IsIEnumerableOfT(this Type type)
        {
            return type.GetInterfaces().Any(x => x.IsGenericType
                   && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        }

        public static object Copy(this object obj)
        {
            Type type = obj.GetType();

            if (type.IsPrimitive)
            {
                return obj;
            }

            if (IsIEnumerableOfT(type))
            {
                return obj;
            }

            //Looking for parameterless constructor if not do not copy
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                return obj;
            }

            var newobj = Activator.CreateInstance(obj.GetType());
            var properties = newobj.GetType().GetProperties();
            var fields = newobj.GetType().GetFields(BindingFlags.Public);

            foreach (var field in fields)
            {
                try
                {
                    object value = field.GetValue(obj);
                    field.SetValue(newobj, value);
                }
                catch (Exception) { }
            }

            foreach (var prop in properties)
            {
                try
                {
                    object value = prop.GetValue(obj);
                    if (prop.CanWrite)
                    {
                        prop.SetValue(newobj, value);
                    }
                }
                catch (Exception) { }
            }

            return newobj;
        }
    }

}