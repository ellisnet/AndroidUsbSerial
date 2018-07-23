// Code originally created by Jeremy Ellis and available from here:
// https://github.com/ellisnet/ArduinoBle/blob/feature/TryingWindows/Source/BluetoothLevel.Net/BluetoothLevel.Net/ViewModels/SimpleViewModel.cs

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
#if NETFX_CORE
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
#else
using System.Windows;
#endif

// ReSharper disable InconsistentNaming
public enum SimpleDialogButtons
{
    OK = 0,
    OKCancel = 1,
    YesNo = 2
}

public enum SimpleDialogResult
{
    None = 0,
    OK = 1,
    Cancel = 2,
    Yes = 3,
    No = 4
}

// ReSharper restore InconsistentNaming

public class SimpleDialog
{

    private string _message = "";

    public string Message
    {
        get => _message;
        set => _message = (value ?? "").Trim();
    }

    private string _title;

    public string Title
    {
        get => _title;
        set => _title = String.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public SimpleDialogButtons Buttons { get; set; }

    public SimpleDialog(string message, string title = null, SimpleDialogButtons buttons = SimpleDialogButtons.OK)
    {
        Message = message;
        Title = title;
        Buttons = buttons;
    }

    public async Task<SimpleDialogResult> ShowAsync()
    {
        var result = SimpleDialogResult.None;

#if NETFX_CORE // ReSharper disable once RedundantAssignment
    string firstButton = null;
    SimpleDialogResult firstButtonResult;
    string secondButton = null;
    SimpleDialogResult secondButtonResult = SimpleDialogResult.None;

    switch (this.Buttons)
    {
        case SimpleDialogButtons.OK:
            firstButton = "OK";
            firstButtonResult = SimpleDialogResult.OK;
            break;
        case SimpleDialogButtons.OKCancel:
            firstButton = "OK";
            firstButtonResult = SimpleDialogResult.OK;
            secondButton = "Cancel";
            secondButtonResult = SimpleDialogResult.Cancel;
            break;
        case SimpleDialogButtons.YesNo:
            firstButton = "Yes";
            firstButtonResult = SimpleDialogResult.Yes;
            secondButton = "No";
            secondButtonResult = SimpleDialogResult.No;
            break;
        default:
            throw new ArgumentOutOfRangeException();
    }

    var dialog = new ContentDialog
    {
        Content = new TextBlock { Text = _message },
        PrimaryButtonText = firstButton,
        IsPrimaryButtonEnabled = true,
        IsSecondaryButtonEnabled = (secondButton != null)
    };
    if (secondButton != null)
    {
        dialog.SecondaryButtonText = secondButton;
    }
    if (_title != null)
    {
        dialog.Title = _title + ":";
    }

    ContentDialogResult dialogResult = await dialog.ShowAsync();
    if (dialogResult == ContentDialogResult.Primary)
    {
        result = firstButtonResult;
    }
    else if (dialogResult == ContentDialogResult.Secondary)
    {
        result = secondButtonResult;
    }

#else

        MessageBoxButton msgButton;
        switch (Buttons)
        {
            case SimpleDialogButtons.OK:
                msgButton = MessageBoxButton.OK;
                break;
            case SimpleDialogButtons.OKCancel:
                msgButton = MessageBoxButton.OKCancel;
                break;
            case SimpleDialogButtons.YesNo:
                msgButton = MessageBoxButton.YesNo;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        MessageBoxResult dialogResult = MessageBox.Show(_message, (_title ?? ""), msgButton);

        switch (dialogResult)
        {
            case MessageBoxResult.OK:
                result = SimpleDialogResult.OK;
                break;
            case MessageBoxResult.Cancel:
                result = SimpleDialogResult.Cancel;
                break;
            case MessageBoxResult.Yes:
                result = SimpleDialogResult.Yes;
                break;
            case MessageBoxResult.No:
                result = SimpleDialogResult.No;
                break;
            default:
                break;
        }

        //satisfy the compiler that something async is happening
        await Task.Run(() => { });

#endif

        return result;
    }
}

public class SimpleViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void NotifyPropertyChanged(string propertyName)
    {
        if ((!String.IsNullOrWhiteSpace(propertyName)))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    protected virtual void ThisPropertyChanged([CallerMemberName] string propertyName = "")
    {
        NotifyPropertyChanged(propertyName);
    }

    protected virtual void SetProperty<T>(ref T property, T newValue, [CallerMemberName] string propertyName = "")
    {
        if (!property.Equals(newValue))
        {
            property = newValue;
            NotifyPropertyChanged(propertyName);
        }
    }

    protected virtual async Task ShowInfo(string message)
    {
        if (!String.IsNullOrWhiteSpace(message))
        {
            SimpleDialogResult result = await (new SimpleDialog(message, "Information", SimpleDialogButtons.OK)).ShowAsync();
        }
    }

    protected virtual async Task ShowError(string message, string details = null)
    {
        if (!String.IsNullOrWhiteSpace(message))
        {
            message = $"An error occurred:\n   {message.Trim()}";
            details = (String.IsNullOrWhiteSpace(details))
                ? ""
                : (details.Trim().Length > 200)
                    ? $"{details.Trim().Substring(0, 195).Trim()}[...]"
                    : details.Trim();
            message += (details == "")
                ? ""
                : $"\n\nDetails:\n{details}";
            SimpleDialogResult result = await (new SimpleDialog(message, "ERROR", SimpleDialogButtons.OK)).ShowAsync();
        }
    }

    protected virtual async Task ShowError(Exception exception, string message = null)
    {
        if (exception != null)
        {
            string msg = (String.IsNullOrWhiteSpace(message))
                ? exception.Message
                : $"{message.Trim()} - {exception.Message}";
            await ShowError(msg, exception.ToString());
        }
    }

#if NETFX_CORE

protected Visibility GetVisibility(bool isVisible)
{
    return isVisible ? Visibility.Visible : Visibility.Collapsed;
}

protected virtual void InvokeOnMainThread(Action functionToExecute)
{
    if (functionToExecute != null)
    {
        // ReSharper disable once UnusedVariable
        var result =
            Window.Current.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, functionToExecute.Invoke);
    }
}

protected virtual Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> functionToExecute)
{
    var completionSource = new TaskCompletionSource<T>();

    // ReSharper disable once UnusedVariable
    var disp = Window.Current.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
        try
        {
            T result = await functionToExecute.Invoke();
            completionSource.SetResult(result);
        }
        catch (Exception ex)
        {
            completionSource.SetException(ex);
        }
    });

    return completionSource.Task;
}

#else

    protected Visibility GetVisibility(bool isVisible)
    {
        return isVisible ? Visibility.Visible : Visibility.Hidden;
    }

    protected virtual void InvokeOnMainThread(Action functionToExecute)
    {
        if (functionToExecute != null)
        {
            Application.Current.Dispatcher.Invoke(functionToExecute.Invoke);
        }
    }

    protected virtual Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> functionToExecute)
    {
        var completionSource = new TaskCompletionSource<T>();

        Application.Current.Dispatcher.Invoke(async () =>
        {
            try
            {
                T result = await functionToExecute.Invoke();
                completionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        });

        return completionSource.Task;
    }

#endif

    public virtual void Dispose()
    {
        // remove event handlers before setting event to null
        Delegate[] delegates = PropertyChanged?.GetInvocationList();
        if (delegates != null)
        {
            foreach (var d in delegates)
            {
                PropertyChanged -= (PropertyChangedEventHandler)d;
            }
        }
        PropertyChanged = null;
    }
}

public class SimpleCommand : ICommand, IDisposable
{
    private Func<object, bool> _canExecuteFunctionWithParam; //allows passing an object parameter to function
    private Action<object> _executeFunctionWithParam; //allows passing an object parameter to function
    private Func<bool> _canExecuteFunctionNoParam; //no parameter passing
    private Action _executeFunctionNoParam; //no parameter passing
    private readonly bool _executeOnMainThread;

    private SimpleCommand(Func<object, bool> canExecuteFunctionWithParam, Action<object> executeFunctionWithParam,
        Func<bool> canExecuteFunctionNoParam, Action executeFunctionNoParam, bool executeOnMainThread)
    {
        _canExecuteFunctionWithParam = canExecuteFunctionWithParam;
        _executeFunctionWithParam = executeFunctionWithParam;
        _canExecuteFunctionNoParam = canExecuteFunctionNoParam;
        _executeFunctionNoParam = executeFunctionNoParam;
        _executeOnMainThread = executeOnMainThread;
    }

    public SimpleCommand(Func<object, bool> canExecuteFunction, Action<object> executeFunction,
        bool executeOnMainThread = false) : this(canExecuteFunction, executeFunction, null, null,
        executeOnMainThread)
    { }

    public SimpleCommand(Func<bool> canExecuteFunction, Action<object> executeFunction,
        bool executeOnMainThread = false) : this(null, executeFunction, canExecuteFunction, null,
        executeOnMainThread)
    { }

    public SimpleCommand(Action<object> executeFunction, bool executeOnMainThread = false) : this(null,
        executeFunction, null, null, executeOnMainThread)
    { }

    public SimpleCommand(Func<object, bool> canExecuteFunction, Action executeFunction,
        bool executeOnMainThread = false) : this(canExecuteFunction, null, null, executeFunction,
        executeOnMainThread)
    { }

    public SimpleCommand(Func<bool> canExecuteFunction, Action executeFunction, bool executeOnMainThread = false) :
        this(null, null, canExecuteFunction, executeFunction, executeOnMainThread)
    { }

    public SimpleCommand(Action executeFunction, bool executeOnMainThread = false) : this(null, null, null,
        executeFunction, executeOnMainThread)
    { }

    public bool CanExecute(object parameter)
    {
        bool result = (_executeFunctionWithParam != null) || (_executeFunctionNoParam != null);

        if (_canExecuteFunctionWithParam != null)
        {
            result = result && _canExecuteFunctionWithParam.Invoke(parameter);
        }
        else if (_canExecuteFunctionNoParam != null)
        {
            result = result && _canExecuteFunctionNoParam.Invoke();
        }

        return result;
    }

    public void Execute(object parameter)
    {
        if (_executeOnMainThread)
        {
#if NETFX_CORE // ReSharper disable UnusedVariable
        if (_executeFunctionWithParam != null)
        {
            var disp1 =
                Window.Current.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { _executeFunctionWithParam.Invoke(parameter); });
        }
        else if (_executeFunctionNoParam != null)
        {
            var disp2 =
                Window.Current.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, _executeFunctionNoParam.Invoke);
        }
        // ReSharper restore UnusedVariable

#else

            if (_executeFunctionWithParam != null)
            {
                Application.Current.Dispatcher.Invoke(() => { _executeFunctionWithParam.Invoke(parameter); });
            }
            else if (_executeFunctionNoParam != null)
            {
                Application.Current.Dispatcher.Invoke(_executeFunctionNoParam.Invoke);
            }

#endif
        }
        else
        {
            //only one of these should be non-null
            _executeFunctionWithParam?.Invoke(parameter);
            _executeFunctionNoParam?.Invoke();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler CanExecuteChanged;

    public void Dispose()
    {
        // remove event handlers before setting event to null
        Delegate[] delegates = CanExecuteChanged?.GetInvocationList();
        if (delegates != null)
        {
            foreach (var d in delegates)
            {
                CanExecuteChanged -= (EventHandler)d;
            }
        }
        CanExecuteChanged = null;

        _canExecuteFunctionWithParam = null;
        _executeFunctionWithParam = null;
        _canExecuteFunctionNoParam = null;
        _executeFunctionNoParam = null;
    }
}
