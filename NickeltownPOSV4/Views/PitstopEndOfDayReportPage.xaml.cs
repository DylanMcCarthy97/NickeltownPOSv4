using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;
using Windows.UI;

namespace NickeltownPOSV4.Views;

public sealed partial class PitstopEndOfDayReportPage : Page
{
    private int _wizardStep;
    private const int LastWizardStep = 2;

    private static readonly SolidColorBrush StepIdleBg = new(Color.FromArgb(255, 243, 245, 250));
    private static readonly SolidColorBrush StepIdleFg = new(Color.FromArgb(255, 107, 114, 128));
    private static readonly SolidColorBrush StepActiveBg = new(Color.FromArgb(255, 37, 99, 235));
    private static readonly SolidColorBrush StepDoneBg = new(Color.FromArgb(255, 219, 234, 254));
    private static readonly SolidColorBrush StepDoneFg = new(Color.FromArgb(255, 29, 78, 216));
    private static readonly SolidColorBrush White = new(Colors.White);

    public PitstopEndOfDayReportPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<PitstopEndOfDayReportViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (DataContext is PitstopEndOfDayReportViewModel vm)
        {
            await vm.InitializeAsync().ConfigureAwait(true);
        }

        SetWizardStep(0);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        App.Services.GetRequiredService<INavigationService>().TryGoBack();
    }

    private void Stage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out var step))
        {
            SetWizardStep(step);
        }
    }

    private void PreviousStep_Click(object sender, RoutedEventArgs e) =>
        SetWizardStep(_wizardStep - 1);

    private void NextStep_Click(object sender, RoutedEventArgs e)
    {
        if (_wizardStep >= LastWizardStep)
        {
            return;
        }

        SetWizardStep(_wizardStep + 1);
    }

    private void SetWizardStep(int step)
    {
        _wizardStep = Math.Clamp(step, 0, LastWizardStep);

        EodSectionSales.Visibility = _wizardStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        EodSectionPrizes.Visibility = _wizardStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        EodSectionCount.Visibility = _wizardStep == 2 ? Visibility.Visible : Visibility.Collapsed;

        WizardPreviousButton.IsEnabled = _wizardStep > 0;
        WizardNextButton.Visibility = _wizardStep < LastWizardStep ? Visibility.Visible : Visibility.Collapsed;
        WizardSaveButton.Visibility = _wizardStep == LastWizardStep ? Visibility.Visible : Visibility.Collapsed;

        StyleStage(StageSales, 0);
        StyleStage(StagePrizes, 1);
        StyleStage(StageCount, 2);

        MainScrollViewer.ChangeView(null, 0, null, true);
    }

    private void StyleStage(Button pill, int stepIndex)
    {
        if (stepIndex == _wizardStep)
        {
            pill.Background = StepActiveBg;
            pill.Foreground = White;
            return;
        }

        if (stepIndex < _wizardStep)
        {
            pill.Background = StepDoneBg;
            pill.Foreground = StepDoneFg;
            return;
        }

        pill.Background = StepIdleBg;
        pill.Foreground = StepIdleFg;
    }
}
