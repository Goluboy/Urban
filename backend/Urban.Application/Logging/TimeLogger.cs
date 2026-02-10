using System.Diagnostics;
using System.Text;

namespace Urban.Application.Logging
{
    public class TimeLogger : IDisposable
    {
        [ThreadStatic]
        private static TimeLogger _currentLogger;

        private readonly string _methodName;
        private readonly Stopwatch _stopwatch;
        private readonly TimeLogger _parent;
        private readonly List<TimeLogger> _children;
        private bool _disposed;

        public TimeLogger(string methodName)
        {
            _methodName = methodName;
            _stopwatch = new Stopwatch();
            _parent = _currentLogger;
            _children = new List<TimeLogger>();
            
            if (_parent != null)
            {
                _parent._children.Add(this);
            }
            
            _currentLogger = this;
            _stopwatch.Start();
        }

        public string Elapsed => _stopwatch.Elapsed.ToString(@"mm\:ss\.fff");

        public static TimeLogger Measure(string methodName)
        {
            return new TimeLogger(methodName);
        }

        public static TimeInfo GetTimeInfo()
        {
            // Find the root logger (the one without parent)
            var root = _currentLogger;
            while (root?._parent != null)
            {
                root = root._parent;
            }
            
            return root?.CreateTimeInfo() ?? new TimeInfo("No measurements", TimeSpan.Zero, new List<TimeInfo>());
        }

        private TimeInfo CreateTimeInfo()
        {
            var elapsed = _stopwatch.Elapsed;
            
            var childrenInfo = new List<TimeInfo>();
            foreach (var child in _children)
            {
                childrenInfo.Add(child.CreateTimeInfo());
            }
            
            return new TimeInfo(_methodName, elapsed, childrenInfo);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _stopwatch.Stop();
                
                // Restore parent as current logger
                _currentLogger = _parent;
            }
        }
    }

    public class TimeInfo
    {
        public string MethodName { get; }
        public TimeSpan Elapsed { get; }
        public List<TimeInfo> Children { get; }
        public TimeSpan SelfTime { get; }

        public TimeInfo(string methodName, TimeSpan elapsed, List<TimeInfo> children)
        {
            MethodName = methodName;
            Elapsed = elapsed;
            Children = children;
            
            // Calculate self time (total time minus children time)
            var childrenTime = TimeSpan.Zero;
            foreach (var child in children)
            {
                childrenTime += child.Elapsed;
            }
            SelfTime = elapsed - childrenTime;
        }

        public override string ToString()
        {
            return ToString(0);
        }

        public string ToString(int indentLevel = 0)
        {
            return ToString(indentLevel, GetMaxMethodNameLength());
        }

        private string ToString(int indentLevel, int maxMethodNameLength)
        {
            var indent = new string(' ', indentLevel * 2);
            var sb = new StringBuilder();
            
            var paddedMethodName = MethodName.PadRight(maxMethodNameLength);
            sb.AppendLine($"{indent}{paddedMethodName} {Elapsed.TotalSeconds:F3} (self: {SelfTime.TotalSeconds:F3})");
            
            foreach (var child in Children)
            {
                sb.Append(child.ToString(indentLevel + 1, maxMethodNameLength));
            }
            
            return sb.ToString();
        }

        private int GetMaxMethodNameLength()
        {
            var maxLength = MethodName.Length;
            foreach (var child in Children)
            {
                maxLength = Math.Max(maxLength, child.GetMaxMethodNameLength());
            }
            return maxLength;
        }
    }
}
