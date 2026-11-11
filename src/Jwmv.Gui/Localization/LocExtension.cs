using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace Jwmv.Gui.Localization;

[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        var source = LocalizationService.Current
            ?? throw new InvalidOperationException("LocalizationService has not been initialized.");

        var binding = new Binding($"[{Key}]")
        {
            Source = source,
            Mode = BindingMode.OneWay
        };

        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target
            && target.TargetObject is not DependencyObject)
        {
            // DataGridTextColumn.Header and similar are not DependencyObjects.
            var textBlock = new TextBlock();
            BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);
            return textBlock;
        }

        return binding.ProvideValue(serviceProvider);
    }
}
