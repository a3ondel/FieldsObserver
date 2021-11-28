
namespace FieldsObserver
{
    public abstract class ObserverAttribute : System.Attribute
    {
        public string[] GroupNames { get; set; }
        public ObserverAttribute(string[] groupNames)
        {
            if (groupNames == null || groupNames?.Length == 0)
            {
                groupNames = new string[] { "default" };
            }

            GroupNames = groupNames;
        }
    }

    public class ObserveField : ObserverAttribute
    {
        public ObserveField(params string[] groupNames) : base(groupNames) { }
    }

    public class InvokeOnChange : ObserverAttribute
    {
        public InvokeOnChange(params string[] groupNames) : base(groupNames) { }
    }

}