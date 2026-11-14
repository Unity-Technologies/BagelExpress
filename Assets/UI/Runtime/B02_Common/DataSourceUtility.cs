using Unity.Properties;
using UnityEngine.UIElements;

namespace Bagel
{
    public static class DataSourceUtility
    {
        /// <summary>
        /// Resolve the object an element is bound to: its hierarchical data source + path
        /// (e.g. a ListView item with AutoAssign is bound to (itemsSource, "[3]")).
        /// </summary>
        public static bool TryResolve<T>(VisualElement element, out T value)
        {
            value = default;
            if (element == null)
                return false;

            var context = element.GetHierarchicalDataSourceContext();
            var source = context.dataSource;
            if (source == null)
                return false;

            if (context.dataSourcePath.IsEmpty)
            {
                if (source is T direct)
                {
                    value = direct;
                    return true;
                }
                return false;
            }

            return PropertyContainer.TryGetValue(ref source, context.dataSourcePath, out value);
        }
    }
}
