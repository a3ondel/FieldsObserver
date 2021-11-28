using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;


public interface IUpdateable
{
    public void Update(object obj);
}
public abstract class UpdaterAtribute : System.Attribute
{
    public string[] GroupNames { get; set; }
    public UpdaterAtribute(string[] groupNames)
    {
        if (groupNames == null || groupNames?.Length == 0)
        {
            groupNames = new string[] { "default" };
        }

        GroupNames = groupNames;
    }
}
public class ObserveField : UpdaterAtribute
{
    public ObserveField(params string[] groupNames) : base(groupNames) { }
}
public class InvokeOnChange : UpdaterAtribute
{
    public InvokeOnChange(params string[] groupNames) : base(groupNames) { }
}

public class UpdaterGroup
{
    public string GroupName { get; set; } = "default";
    public List<FieldInfo> Fields { get; set; } = new List<FieldInfo>();

    public List<Action> MethodsToInove { get; set; } = new List<Action>();

    public bool Changed { get; set; } = false;

    public bool ContainsField(FieldInfo field) => Fields.Contains(field);
    public override string ToString()
    {
        return GroupName;
    }
}

public class FieldUpdater<T>
{
    private class HistoryObject
    {
        public object actualValue { get; set; }
        public object lastValue { get; set; }

        public void Update(object actualValue)
        {
            lastValue = this.actualValue; //TODO to change
            this.actualValue = actualValue;
        }


        public HistoryObject(object value)
        {
            IConvertible convertable = value as IConvertible;

            IUpdateable[] enumerable = value as IUpdateable[];

            if (enumerable != null)
            {
                actualValue = Helper.Cast(value, value.GetType());
                var tmp1 = new List<IUpdateable>(enumerable.Length);
                var tmp2 = new List<IUpdateable>(enumerable.Length);
                for (int i = 0; i < enumerable.Length; i++)
                {
                    tmp1.Add(new BaseClass());
                    tmp2.Add(new BaseClass());
                }

                actualValue = tmp1.ToArray();
                lastValue = tmp2.ToArray();

                for (int i = 0; i < enumerable.Length; i++)
                {
                    var instance = Activator.CreateInstance(enumerable[0].GetType()) as IUpdateable;
                    instance.Update(enumerable[i]);
                    (lastValue as IUpdateable[])[i] = instance;
                }

            }
            else if (!value.GetType().IsPrimitive || convertable != null)
            {
                actualValue = Helper.Cast(value, value.GetType());
                lastValue = Helper.Cast(value, value.GetType());
            }
            else
            {
                actualValue = value;
                lastValue = value;
            }
            var res = actualValue == lastValue;
        }

    }
    private readonly Dictionary<string, UpdaterGroup> _groups = new Dictionary<string, UpdaterGroup>() { };
    private readonly Dictionary<string, HistoryObject> _fieldsValueHistory = new Dictionary<string, HistoryObject>();
    private readonly List<FieldInfo> _allfieldsToUpdate;
    private readonly object _classToUpdate;
    public FieldUpdater(object classToUpdate)
    {
        _classToUpdate = classToUpdate;
        _groups.Add("default", new UpdaterGroup());

        _allfieldsToUpdate = typeof(T).GetFields()
                                      .Where(field => field.GetCustomAttributes(typeof(ObserveField), false).Length > 0)
                                      .ToList();

        _allfieldsToUpdate.ForEach(field =>
        {
            var groupNames = field.GetCustomAttribute<ObserveField>().GroupNames;
            foreach (var groupName in groupNames)
            {
                if (!_groups.ContainsKey(groupName))
                {
                    _groups[groupName] = new UpdaterGroup() { GroupName = groupName };
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
                        _groups[groupName] = new UpdaterGroup() { GroupName = groupName };
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
            var actualFieldValue = field.GetValue(_classToUpdate);

            var type = historyValue.lastValue.GetType();
            var castedLastValue = Helper.Cast(historyValue.lastValue, type);


            if (Helper.IsIEnumerableOfT(type))
            {
                bool changed = false;

                for (int i = 0; i < castedLastValue.Length; i++)
                {
                    if (!castedLastValue[i].Equals((historyValue.actualValue as object[])[i]))
                    {
                        changed = true;
                        break;
                    }
                }

                if (changed)
                {
                    var elementType = castedLastValue[0].GetType();
                    for (int i = 0; i < castedLastValue.Length; i++)
                    {
                        var newInstance = Activator.CreateInstance(elementType) as IUpdateable;
                        newInstance.Update(castedLastValue[i]);
                        castedLastValue[i] = newInstance;

                        historyValue.actualValue = actualFieldValue;
                    }

                    foreach (var group in _groups)
                    {
                        group.Value.Changed = group.Value.ContainsField(field);
                    }
                }
            }
            else
            if (!castedLastValue.Equals(actualFieldValue))
            {
                _fieldsValueHistory[field.Name].Update(actualFieldValue);

                foreach (var group in _groups)
                {
                    group.Value.Changed = group.Value.ContainsField(field);
                }
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
}

public static class Helper
{
    public static bool IsNumericType(this object o)
    {
        switch (Type.GetTypeCode(o.GetType()))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return true;
            default:
                return false;
        }
    }
    public static dynamic Cast(dynamic obj, Type castTo)
    {
        return Convert.ChangeType(obj, castTo);
    }

    public static bool IsIEnumerableOfT(this Type type)
    {
        return type.GetInterfaces().Any(x => x.IsGenericType
               && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }
}


