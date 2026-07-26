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
    private const int LastWizardStep = 6;

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

    private void StepPill_Click(object sender, RoutedEventArgs e)
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

        EodSectionSummary.Visibility = _wizardStep is 0 or 6 ? Visibility.Visible : Visibility.Collapsed;
        EodSectionEvent.Visibility = _wizardStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        EodSectionPos.Visibility = _wizardStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        EodSectionSquare.Visibility = _wizardStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        EodSectionOutsideTerminalSales.Visibility = _wizardStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        EodSectionCosts.Visibility = _wizardStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        EodSectionPrizes.Visibility = _wizardStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        EodSectionCashCount.Visibility = _wizardStep == 5 ? Visibility.Visible : Visibility.Collapsed;
        EodReviewActions.Visibility = _wizardStep == 6 ? Visibility.Visible : Visibility.Collapsed;

        WizardPreviousButton.IsEnabled = _wizardStep > 0;
        WizardNextButton.Visibility = _wizardStep < LastWizardStep ? Visibility.Visible : Visibility.Collapsed;
        WizardSaveButton.Visibility = _wizardStep == LastWizardStep ? Visibility.Visible : Visibility.Collapsed;

        WizardStepTitle.Text = _wizardStep switch
        {
            0 => "Step 1 — Today’s event",
            1 => "Step 2 — Inside till (Terminal 0070)",
            2 => "Step 3 — Outside merch tin (Flounderers02)",
            3 => "Step 4 — Floats and cash paid out",
            4 => "Step 5 — Prizes given from stock",
            5 => "Step 6 — Count the cash",
            _ => "Step 7 — Check it, then save",
        };
        WizardStepHint.Text = _wizardStep switch
        {
            0 => "Check the event name and the date are right, then tap Next.",
            1 => "This fills in by itself. Just check the cash and card totals look right.",
            2 => "Copy the cash amounts off the paper merch sheet. Card comes from Square.",
            3 => "Type in the cash you started with, and any cash you took back out.",
            4 => "Only if you gave merch away. Skip this if you didn’t.",
            5 => "Count the money in the till and in the tin, then type each total in.",
            _ => "Check the money to bank looks right, then tap Save report.",
        };

        WizardProgressBar.Value = ((_wizardStep + 1) / (double)(LastWizardStep + 1)) * 100.0;
        WizardStepCounter.Text = $"{_wizardStep + 1} / {LastWizardStep + 1}";

        StyleStepPill(StepPill0, 0);
        StyleStepPill(StepPill1, 1);
        StyleStepPill(StepPill2, 2);
        StyleStepPill(StepPill3, 3);
        StyleStepPill(StepPill4, 4);
        StyleStepPill(StepPill5, 5);
        StyleStepPill(StepPill6, 6);

        MainScrollViewer.ChangeView(null, 0, null, true);
    }

    private void StyleStepPill(Button pill, int stepIndex)
    {
        if (stepIndex == _wizardStep)
        {
            pill.Background = StepActiveBg;
            pill.Foreground = White;
            pill.BorderBrush = StepActiveBg;
            return;
        }

        if (stepIndex < _wizardStep)
        {
            pill.Background = StepDoneBg;
            pill.Foreground = StepDoneFg;
            pill.BorderBrush = StepDoneBg;
            return;
        }

        pill.Background = StepIdleBg;
        pill.Foreground = StepIdleFg;
        pill.BorderBrush = StepIdleBg;
    }
}
