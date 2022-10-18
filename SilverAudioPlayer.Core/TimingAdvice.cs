
using ArxOne.MrAdvice.Advice;
using Serilog;
using System.Diagnostics;

namespace SilverAudioPlayer.Core
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