using System.Globalization;
using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 문서 표시 배율 (01 F-06, 02 §17.2). 버튼·단축키는 정해진 단계를 따르고, `Ctrl+휠`은 뷰어(WebView2)가 자체 단계로 바꾼 값을
/// <see cref="OnViewerZoomChanged"/>로 받는다. 모든 탭과 도움말 창이 같은 배율을 쓴다. UI 스레드에서 쓴다.
/// </summary>
public sealed class ZoomViewModel : ObservableObject
{
    private static readonly double MinFactor = AppConstants.ZoomSteps[0];
    private static readonly double MaxFactor = AppConstants.ZoomSteps[^1];

    private double _factor = AppConstants.DefaultZoomFactor;

    public ZoomViewModel()
    {
        ZoomInCommand = new RelayCommand(() => SetFromApp(NextStep(_factor)), () => _factor < MaxFactor - AppConstants.ZoomStepTolerance);
        ZoomOutCommand = new RelayCommand(() => SetFromApp(PreviousStep(_factor)), () => _factor > MinFactor + AppConstants.ZoomStepTolerance);
        ResetCommand = new RelayCommand(() => SetFromApp(AppConstants.DefaultZoomFactor));
    }

    /// <summary>앱이 배율을 정했다 — 뷰어에 넣어야 한다. 뷰어가 스스로 바꾼 값(범위 안)은 알리지 않는다(되돌아오며 맴돌지 않게).</summary>
    public event EventHandler<double>? FactorChanged;

    public double Factor
    {
        get => _factor;
        private set
        {
            if (SetProperty(ref _factor, value))
            {
                OnPropertyChanged(nameof(PercentText));
                ZoomInCommand.NotifyCanExecuteChanged();
                ZoomOutCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>툴바 가운데 표시(정수 %).</summary>
    public string PercentText => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(_factor * 100):0}%");

    public RelayCommand ZoomInCommand { get; }

    public RelayCommand ZoomOutCommand { get; }

    /// <summary>100%로 (가운데 표시 클릭, `Ctrl+0`).</summary>
    public RelayCommand ResetCommand { get; }

    /// <summary>
    /// 뷰어가 `Ctrl+휠`로 배율을 바꿨다. 단계 사이 값이면 그대로 보여 주고, 50%~300% 밖이면 가까운 끝 값으로 되돌려 뷰어에 다시 넣게 한다.
    /// </summary>
    public void OnViewerZoomChanged(double factor)
    {
        var clamped = Math.Clamp(factor, MinFactor, MaxFactor);
        if (Math.Abs(clamped - factor) > AppConstants.ZoomStepTolerance)
        {
            SetFromApp(clamped);
            return;
        }

        Factor = factor;
    }

    private void SetFromApp(double factor)
    {
        Factor = factor;
        FactorChanged?.Invoke(this, factor);
    }

    private static double NextStep(double current) =>
        AppConstants.ZoomSteps.FirstOrDefault(step => step > current + AppConstants.ZoomStepTolerance, MaxFactor);

    private static double PreviousStep(double current) =>
        AppConstants.ZoomSteps.LastOrDefault(step => step < current - AppConstants.ZoomStepTolerance, MinFactor);
}
