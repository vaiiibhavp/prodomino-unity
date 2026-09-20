using Cysharp.Threading.Tasks;

namespace Timba.Utils
{
    // ## Script used to contain delegates used in the project ##
    public delegate UniTask AsyncActionHandler();
    public delegate UniTask AsyncActionHandler<TEventArgs>(TEventArgs e);
    public delegate UniTask AsyncActionHandler<TEventArgs, TEventArgs2>(TEventArgs e, TEventArgs2 f);
    public delegate UniTask AsyncActionHandler<TEventArgs, TEventArgs2, TEventArgs3>(TEventArgs e, TEventArgs2 f, TEventArgs3 g);
    public delegate UniTask AsyncActionHandler<TEventArgs, TEventArgs2, TEventArgs3, TEventArgs4>(TEventArgs e, TEventArgs2 f, TEventArgs3 g, TEventArgs4 h);

    public delegate UniTask<T> AsyncFuncHandler<T>();
    public delegate UniTask<T> AsyncFuncHandler<T, TEventArgs>(TEventArgs e);
    public delegate UniTask<T> AsyncFuncHandler<T, TEventArgs, TEventArgs2>(TEventArgs e, TEventArgs2 f);
    public delegate UniTask<T> AsyncFuncHandler<T, TEventArgs, TEventArgs2, TEventArgs3>(TEventArgs e, TEventArgs2 f, TEventArgs3 g);
}
