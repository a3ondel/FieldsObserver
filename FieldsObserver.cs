using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System;

//this interface 
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

public class FieldsObserver<T>
{
    private class HistoryObject
    {
        public object actualValue { get; set; }
        public object lastValue { get; set; }

        public void Update(object actualValue)
        {
            lastValue = this.actualValue; //TODO it should be a copy
            this.actualValue = actualValue;
        }


        public HistoryObject(object value)
        {
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
    public FieldsObserver(object classToUpdate)
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
            var actualFieldValue = field.GetValue(_classToUpdate);//always return new referance

            var type = historyValue.lastValue.GetType();
            var castedLastValue = Helper.Cast(historyValue.lastValue, type);
            bool changed = false;

            if (Helper.IsIEnumerableOfT(type))
            {
                for (int i = 0; i < castedLastValue.Length; i++)
                {
                    if (!castedLastValue[i].Equals((historyValue.actualValue as object[])[i]))
                    {
                        changed = true;
                        break;
                    }
                }
            }
            else if (!castedLastValue.Equals(actualFieldValue))
            {
                changed = true;
            }

            if (changed)
            {
                _fieldsValueHistory[field.Name].Update(actualFieldValue);
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
}