using ArxOne.MrAdvice.Advice;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FuzzySharp;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverConfig.CobaltExtensions;
using SilverCraft.AvaloniaUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Avalonia
{
    [AttributeUsage(AttributeTargets.Method)]
    public class TimingAdvice : Attribute, IMethodAdvice
    {
        public void Advise(MethodAdviceContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            context.Proceed();
            stopwatch.Stop();
            Log.Information("{MethodName} took {Duration} to execute", context.TargetName, stopwatch.Elapsed);
        }
    }
}