using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services.Logging;
using NickeltownPOSV4.ViewModels;



namespace NickeltownPOSV4.Views;



public sealed partial class WorkspacePage : Page

{

    public static readonly DependencyProperty PageTitleProperty = DependencyProperty.Register(

        nameof(PageTitle),

        typeof(string),

        typeof(WorkspacePage),

        new PropertyMetadata(string.Empty));



    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(

        nameof(Subtitle),

        typeof(string),

        typeof(WorkspacePage),

        new PropertyMetadata(string.Empty));



    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(

        nameof(Body),

        typeof(string),

        typeof(WorkspacePage),

        new PropertyMetadata(string.Empty));



    public WorkspacePage()
    {
        LoginTransitionProbe.Mark("workspace.ctor.begin");
        InitializeComponent();
        LoginTransitionProbe.Mark("workspace.ctor.xaml");

        var vm = App.Services.GetRequiredService<WorkspacePageViewModel>();

        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        SyncFromViewModel(vm);
        LoginTransitionProbe.Mark("workspace.ctor.done");
    }



    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)

    {

        if (sender is not WorkspacePageViewModel vm)

        {

            return;

        }



        if (e.PropertyName is nameof(WorkspacePageViewModel.IsTabsRoute)
            or nameof(WorkspacePageViewModel.IsPitstopRoute))

        {

            UpdateRouteLayout(vm);

            return;

        }



        if (e.PropertyName is nameof(WorkspacePageViewModel.PageTitle)

            or nameof(WorkspacePageViewModel.Subtitle)

            or nameof(WorkspacePageViewModel.Body))

        {

            SyncFromViewModel(vm);

        }

    }



    public string PageTitle

    {

        get => (string)GetValue(PageTitleProperty);

        set => SetValue(PageTitleProperty, value);

    }



    public string Subtitle

    {

        get => (string)GetValue(SubtitleProperty);

        set => SetValue(SubtitleProperty, value);

    }



    public string Body

    {

        get => (string)GetValue(BodyProperty);

        set => SetValue(BodyProperty, value);

    }



    protected override void OnNavigatedTo(NavigationEventArgs e)

    {

        base.OnNavigatedTo(e);



        if (DataContext is WorkspacePageViewModel vm && e.Parameter is ShellRoute route)

        {

            vm.ApplyRoute(route);

            SyncFromViewModel(vm);

        }

    }



    private void SyncFromViewModel(WorkspacePageViewModel vm)

    {

        PageTitle = vm.PageTitle;

        Subtitle = vm.Subtitle;

        Body = vm.Body;

        UpdateRouteLayout(vm);

    }



    private bool _pitstopHostRealized;

    private void UpdateRouteLayout(WorkspacePageViewModel vm)
    {
        if (vm.IsTabsRoute)
        {
            ClassicWorkspacePanel.Visibility = Visibility.Collapsed;
            TabsWorkspaceHost.Visibility = Visibility.Visible;
            CollapsePitstopHost();
            return;
        }

        if (vm.IsPitstopRoute)
        {
            ClassicWorkspacePanel.Visibility = Visibility.Collapsed;
            TabsWorkspaceHost.Visibility = Visibility.Collapsed;
            EnsurePitstopRetailHost();
            if (_pitstopHostRealized)
            {
                PitstopRetailHost.Visibility = Visibility.Visible;
            }

            var pitVm = App.Services.GetRequiredService<PitstopRetailViewModel>();
            _ = pitVm.RefreshCatalogFromDatabaseAsync();
            return;
        }

        ClassicWorkspacePanel.Visibility = Visibility.Visible;
        TabsWorkspaceHost.Visibility = Visibility.Collapsed;
        CollapsePitstopHost();
    }

    private void EnsurePitstopRetailHost()
    {
        if (_pitstopHostRealized)
        {
            return;
        }

        LoginTransitionProbe.Mark("pitstop.realize.begin");
        _ = FindName(nameof(PitstopRetailHost));
        _pitstopHostRealized = true;
        LoginTransitionProbe.Mark("pitstop.realize.done");
    }

    private void CollapsePitstopHost()
    {
        if (!_pitstopHostRealized)
        {
            return;
        }

        PitstopRetailHost.Visibility = Visibility.Collapsed;
    }

}


