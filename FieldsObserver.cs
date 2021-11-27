using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System;

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



public class FieldsObserver<T>
{
    private class UpdaterGroup
    {
        public List<FieldInfo> Fields { get; } = new List<FieldInfo>();

        public List<Action> MethodsToInove { get; } = new List<Action>();

        public bool Changed { get; set; } = false;

        public bool ContainsField(FieldInfo field) => Fields.Contains(field);

    }

    private readonly Dictionary<string, UpdaterGroup> _groups = new Dictionary<string, UpdaterGroup>() { };
    private readonly Dictionary<string, object> _fieldsValueHistory = new Dictionary<string, object>();
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
                    _groups[groupName] = new UpdaterGroup();
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
                        _groups[groupName] = new UpdaterGroup();
                    }
                    _groups[groupName].MethodsToInove.Add(Delegate.CreateDelegate(typeof(Action), classToUpdate, method) as Action);
                }
            }
        });

        foreach (var field in _allfieldsToUpdate)
        {
            //Adding starting values to history
            _fieldsValueHistory.Add(field.Name, field.GetValue(_classToUpdate));
        }
    }

    public void CheckForUpdates()
    {
        foreach (var field in _allfieldsToUpdate)
        {
            _fieldsValueHistory.TryGetValue(field.Name, out var historyValue);
            var actualFieldValue = field.GetValue(_classToUpdate);
            if (!actualFieldValue.Equals(historyValue))
            {
                _fieldsValueHistory[field.Name] = actualFieldValue;

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
