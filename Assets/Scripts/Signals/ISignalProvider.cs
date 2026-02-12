// Assets/Scripts/Signals/ISignalProvider.cs
using MappingTool;

namespace Signals
{
    public interface ISignalProvider
    {
        bool TryGetSignal(InputSignal signal, out float value);
    }
}
