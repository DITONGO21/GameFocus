using System;
using System.Threading.Tasks;

namespace GameFocus.Application.Interfaces;

public interface IUIDispatcher
{
    void Enqueue(Action action);
}
