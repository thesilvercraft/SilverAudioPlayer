using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using Avalonia.Controls;
using AvaloniaEdit;
using Serilog;

namespace Catenoid;

public partial class RawMetadataView : Window
{
    public RawMetadataView()
    {
        InitializeComponent();
        this.DoAfterInitTasksF();
    }

    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
            for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                var propType = typeInfo.Properties[i].PropertyType;
                if (IsUnsupportedSerializationType(propType))
                {
                    typeInfo.Properties.RemoveAt(i);
                }
            }
        });

        return new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            MaxDepth = 16,
            TypeInfoResolver = resolver
        };
    }

    private static bool IsUnsupportedSerializationType(Type t)
    {
        return typeof(Stream).IsAssignableFrom(t)
               || typeof(Delegate).IsAssignableFrom(t)
               || typeof(MemberInfo).IsAssignableFrom(t)
               || typeof(WaitHandle).IsAssignableFrom(t)
               || t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IObservable<>));
    }

    public RawMetadataView(SilverAudioPlayer.Shared.Song currentSong) : this()
    {
        var textBox = this.FindControl<TextEditor>("TextBox");
        if (textBox == null) return;
        try
        {
            textBox.Text = JsonSerializer.Serialize(currentSong, SerializerOptions) ?? "No Raw Metadata";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to serialize raw metadata");
            textBox.Text = $"Failed to serialize raw metadata: {ex.Message}";
        }
    }
}