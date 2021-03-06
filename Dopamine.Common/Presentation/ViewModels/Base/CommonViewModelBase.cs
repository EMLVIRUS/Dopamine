﻿using Digimezzo.Utilities.Settings;
using Digimezzo.Utilities.Utils;
using Dopamine.Common.Base;
using Dopamine.Common.Database;
using Dopamine.Common.Presentation.Views;
using Dopamine.Common.Services.Collection;
using Dopamine.Common.Services.Dialog;
using Dopamine.Common.Services.I18n;
using Dopamine.Common.Services.Indexing;
using Dopamine.Common.Services.Metadata;
using Dopamine.Common.Services.Playback;
using Dopamine.Common.Services.Playlist;
using Dopamine.Common.Services.Provider;
using Dopamine.Common.Services.Search;
using Dopamine.Common.Utils;
using Microsoft.Practices.Unity;
using Prism;
using Prism.Commands;
using Prism.Events;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Dopamine.Common.Presentation.ViewModels.Base
{
    public abstract class CommonViewModelBase : ViewModelBase, INavigationAware, IActiveAware
    {
        #region Variables
        // Collections
        private ObservableCollection<PlaylistViewModel> contextMenuPlaylists;
        private ObservableCollection<SearchProvider> contextMenuSearchProviders;

        // Services
        private IIndexingService indexingService;
        private ICollectionService collectionService;
        private IMetadataService metadataService;
        private II18nService i18nService;
        private IPlaybackService playbackService;
        private IDialogService dialogService;
        private ISearchService searchService;
        private IPlaylistService playlistService;

        // EventAggregator
        private IEventAggregator eventAggregator;

        // Flags
        private bool enableRating;
        private bool enableLove;
        private bool isFirstLoad = true;
        private bool isIndexing;

        // Counts
        private long tracksCount;
        private long totalDuration;
        private long totalSize;

        // Other
        private TrackOrder trackOrder;
        private string trackOrderText;
        private string searchTextBeforeInactivate = string.Empty;
        #endregion

        #region Commands
        public DelegateCommand ToggleTrackOrderCommand { get; set; }
        public DelegateCommand RemoveSelectedTracksCommand { get; set; }
        public DelegateCommand RemoveSelectedTracksFromDiskCommand { get; set; }
        public DelegateCommand<string> AddTracksToPlaylistCommand { get; set; }
        public DelegateCommand ShowSelectedTrackInformationCommand { get; set; }
        public DelegateCommand<object> SelectedTracksCommand { get; set; }
        public DelegateCommand EditTracksCommand { get; set; }
        public DelegateCommand PlayNextCommand { get; set; }
        public DelegateCommand AddTracksToNowPlayingCommand { get; set; }
        public DelegateCommand ShuffleAllCommand { get; set; }
        public DelegateCommand LoadedCommand { get; set; }
        #endregion

        #region Properties
        public IIndexingService IndexingService
        {
            get
            {
                return this.indexingService;
            }
        }

        public ICollectionService CollectionService
        {
            get
            {
                return this.collectionService;
            }
        }

        public IMetadataService MetadataService
        {
            get
            {
                return this.metadataService;
            }
        }

        public II18nService I18nService
        {
            get
            {
                return this.i18nService;
            }
        }

        public IPlaybackService PlaybackService
        {
            get
            {
                return this.playbackService;
            }
        }

        public IDialogService DialogService
        {
            get
            {
                return this.dialogService;
            }
        }

        public ISearchService SearchService
        {
            get
            {
                return this.searchService;
            }
        }

        public IPlaylistService PlaylistService
        {
            get
            {
                return this.playlistService;
            }
        }

        protected IEventAggregator EventAggregator
        {
            get
            {
                return eventAggregator;
            }
        }

        public long TracksCount
        {
            get { return this.tracksCount; }
            set { SetProperty<long>(ref this.tracksCount, value); }
        }

        public string TotalSizeInformation
        {
            get { return this.totalSize > 0 ? FormatUtils.FormatFileSize(this.totalSize, false) : string.Empty; }
        }

        public string TotalDurationInformation
        {
            get { return this.totalDuration > 0 ? FormatUtils.FormatDuration(this.totalDuration) : string.Empty; }
        }

        public bool EnableRating
        {
            get { return this.enableRating; }
            set { SetProperty<bool>(ref this.enableRating, value); }
        }

        public bool EnableLove
        {
            get { return this.enableLove; }
            set { SetProperty<bool>(ref this.enableLove, value); }
        }

        public bool HasContextMenuPlaylists
        {
            get { return this.ContextMenuPlaylists != null && this.ContextMenuPlaylists.Count > 0; }
        }

        public string TrackOrderText
        {
            get { return this.trackOrderText; }
        }

        public ObservableCollection<PlaylistViewModel> ContextMenuPlaylists
        {
            get { return this.contextMenuPlaylists; }
            set
            {
                SetProperty<ObservableCollection<PlaylistViewModel>>(ref this.contextMenuPlaylists, value);
                OnPropertyChanged(() => this.HasContextMenuPlaylists);
            }
        }

        public bool IsIndexing
        {
            get { return this.isIndexing; }
            set { SetProperty<bool>(ref this.isIndexing, value); }
        }

        public TrackOrder TrackOrder
        {
            get { return this.trackOrder; }
            set
            {
                SetProperty<TrackOrder>(ref this.trackOrder, value);
                this.UpdateTrackOrderText(value);
            }
        }

        public bool IsActive
        {
            get { return this.isActive; }
            set { SetProperty<bool>(ref this.isActive, value); }
        }
        #endregion

        #region Construction
        public CommonViewModelBase(IUnityContainer container) : base(container)
        {
            // EventAggregator
            this.eventAggregator = container.Resolve<IEventAggregator>();

            // Services
            this.indexingService = container.Resolve<IIndexingService>();
            this.playbackService = container.Resolve<IPlaybackService>();
            this.searchService = container.Resolve<ISearchService>();
            this.dialogService = container.Resolve<IDialogService>();
            this.collectionService = container.Resolve<ICollectionService>();
            this.metadataService = container.Resolve<IMetadataService>();
            this.i18nService = container.Resolve<II18nService>();
            this.playlistService = container.Resolve<IPlaylistService>();

            // Handlers
            this.ProviderService.SearchProvidersChanged += (_, __) => { this.GetSearchProvidersAsync(); };

            // Initialize the search providers in the ContextMenu
            this.GetSearchProvidersAsync();

            // Initialize
            this.Initialize();
        }
        #endregion

        #region Private
        private void Initialize()
        {
            // Commands
            this.ShowSelectedTrackInformationCommand = new DelegateCommand(() => this.ShowSelectedTrackInformation());
            this.SelectedTracksCommand = new DelegateCommand<object>((parameter) => this.SelectedTracksHandler(parameter));
            this.EditTracksCommand = new DelegateCommand(() => this.EditSelectedTracks(), () => !this.IsIndexing);
            this.LoadedCommand = new DelegateCommand(async () => await this.LoadedCommandAsync());
            this.ShuffleAllCommand = new DelegateCommand(() => this.playbackService.EnqueueAsync(true, false)); 

            // Events
            this.playbackService.PlaybackFailed += (_, __) => this.ShowPlayingTrackAsync();
            this.playbackService.PlaybackPaused += (_, __) => this.ShowPlayingTrackAsync();
            this.playbackService.PlaybackResumed += (_, __) => this.ShowPlayingTrackAsync();
            this.playbackService.PlaybackStopped += (_, __) => this.ShowPlayingTrackAsync();
            this.playbackService.PlaybackSuccess += (_) => this.ShowPlayingTrackAsync();

            this.collectionService.CollectionChanged += async (_, __) => await this.FillListsAsync(); // Refreshes the lists when the Collection has changed
            this.playlistService.PlaylistAdded += (_) => this.GetContextMenuPlaylistsAsync();
            this.playlistService.PlaylistDeleted += (_) => this.GetContextMenuPlaylistsAsync();

            this.indexingService.RefreshLists += async (_, __) => await this.FillListsAsync(); // Refreshes the lists when the indexer has finished indexing
            this.indexingService.IndexingStarted += (_, __) => this.SetEditCommands();
            this.indexingService.IndexingStopped += (_, __) => this.SetEditCommands();

            this.searchService.DoSearch += (searchText) => { if (this.IsActive) this.FilterLists(); };

            this.metadataService.RatingChanged += MetadataService_RatingChangedAsync;
            this.metadataService.LoveChanged += MetadataService_LoveChangedAsync;

            // Flags
            this.EnableRating = SettingsClient.Get<bool>("Behaviour", "EnableRating");
            this.EnableLove = SettingsClient.Get<bool>("Behaviour", "EnableLove");

            // This makes sure the IsIndexing is correct even when this ViewModel is 
            // created after the Indexer is started, and thus after triggering the 
            // IndexingService.IndexerStarted event.
            this.SetEditCommands();

            // Initialize the playlists in the ContextMenu
            this.GetContextMenuPlaylistsAsync();
        }

        private async void GetContextMenuPlaylistsAsync()
        {
            try
            {
                // Unbind to improve UI performance
                this.ContextMenuPlaylists = null;

                List<string> playlists = await this.playlistService.GetPlaylistsAsync();

                // Populate an ObservableCollection
                var playlistViewModels = new ObservableCollection<PlaylistViewModel>();

                await Task.Run(() =>
                {
                    foreach (string playlist in playlists)
                    {
                        playlistViewModels.Add(new PlaylistViewModel { Name = playlist });
                    }

                });

                // Re-bind to update the UI
                this.ContextMenuPlaylists = playlistViewModels;

            }
            catch (Exception)
            {
                // If loading from the database failed, create and empty Collection.
                this.ContextMenuPlaylists = new ObservableCollection<PlaylistViewModel>();
            }
        }

        private async void GetSearchProvidersAsync()
        {
            this.ContextMenuSearchProviders = null;

            List<SearchProvider> providersList = await this.ProviderService.GetSearchProvidersAsync();
            var localProviders = new ObservableCollection<SearchProvider>();

            await Task.Run(() =>
            {
                foreach (SearchProvider vp in providersList)
                {
                    localProviders.Add(vp);
                }
            });

            this.ContextMenuSearchProviders = localProviders;
        }
        #endregion

        #region Protected
        protected bool IsFirstLoad()
        {
            bool originalIsFirstLoad = this.isFirstLoad;
            if (this.isFirstLoad) this.isFirstLoad = false;

            return originalIsFirstLoad;
        }

        protected void SetSizeInformation(long totalDuration, long totalSize)
        {
            this.totalDuration = totalDuration;
            this.totalSize = totalSize;
        }

        protected void UpdateTrackOrderText(TrackOrder trackOrder)
        {
            switch (trackOrder)
            {
                case TrackOrder.Alphabetical:
                    this.trackOrderText = ResourceUtils.GetStringResource("Language_A_Z");
                    break;
                case TrackOrder.ReverseAlphabetical:
                    this.trackOrderText = ResourceUtils.GetStringResource("Language_Z_A");
                    break;
                case TrackOrder.ByAlbum:
                    this.trackOrderText = ResourceUtils.GetStringResource("Language_By_Album");
                    break;
                case TrackOrder.ByRating:
                    this.trackOrderText = ResourceUtils.GetStringResource("Language_By_Rating");
                    break;
                default:
                    // Cannot happen, but just in case.
                    this.trackOrderText = ResourceUtils.GetStringResource("Language_By_Album");
                    break;
            }

            OnPropertyChanged(() => this.TrackOrderText);
        }

        protected bool CheckAllSelectedFilesExist(List<string> paths)
        {
            bool allSelectedTracksExist = true;

            foreach (string path in paths)
            {
                if (!System.IO.File.Exists(path))
                {
                    allSelectedTracksExist = false;
                    break;
                }
            }

            if (!allSelectedTracksExist)
            {
                string message = ResourceUtils.GetStringResource("Language_Song_Cannot_Be_Found_Refresh_Collection");
                if (paths.Count > 1) message = ResourceUtils.GetStringResource("Language_Songs_Cannot_Be_Found_Refresh_Collection");

                if (this.dialogService.ShowConfirmation(0xe11b, 16, ResourceUtils.GetStringResource("Language_Refresh"), message, ResourceUtils.GetStringResource("Language_Yes"), ResourceUtils.GetStringResource("Language_No")))
                {
                    this.indexingService.NeedsIndexing = true;
                    this.indexingService.IndexCollectionAsync(SettingsClient.Get<bool>("Indexing", "IgnoreRemovedFiles"), false);
                }
            }

            return allSelectedTracksExist;
        }

        protected void ShowFileInformation(List<string> paths)
        {
            if (this.CheckAllSelectedFilesExist(paths))
            {
                Views.FileInformation view = this.Container.Resolve<Views.FileInformation>();
                view.DataContext = this.Container.Resolve<FileInformationViewModel>(new DependencyOverride(typeof(string), paths.First()));

                this.dialogService.ShowCustomDialog(
                    0xe8d6,
                    16,
                    ResourceUtils.GetStringResource("Language_Information"),
                    view,
                    400,
                    620,
                    true,
                    true,
                    true,
                    false,
                    ResourceUtils.GetStringResource("Language_Ok"),
                    string.Empty,
                    null);
            }
        }

        protected void EditFiles(List<string> paths)
        {
            if (this.CheckAllSelectedFilesExist(paths))
            {
                EditTrack view = this.Container.Resolve<EditTrack>();
                view.DataContext = this.Container.Resolve<EditTrackViewModel>(new DependencyOverride(typeof(IList<string>), paths));

                this.dialogService.ShowCustomDialog(
                    0xe104,
                    14,
                    ResourceUtils.GetStringResource("Language_Edit_Song"),
                    view,
                    620,
                    660,
                    false,
                    false,
                    false,
                    true,
                    ResourceUtils.GetStringResource("Language_Ok"),
                    ResourceUtils.GetStringResource("Language_Cancel"),
                ((EditTrackViewModel)view.DataContext).SaveTracksAsync);
            }
        }
        #endregion

        #region IActiveAware
        private bool isActive;
        public event EventHandler IsActiveChanged;
        #endregion

        #region INavigationAware
        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            this.Unsubscribe();
            this.searchTextBeforeInactivate = this.searchService.SearchText;
        }

        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            this.Subscribe();

            // Only refresh the Tracks if the search term was changed since the last time this screen was visited
            if (!this.searchTextBeforeInactivate.Equals(this.searchService.SearchText))
            {
                await Task.Delay(Constants.CommonListLoadDelay); // Wait for the UI to slide in
                this.FilterLists();
            }

            this.ConditionalScrollToPlayingTrack();
        }
        #endregion

        #region Virtual
        protected virtual void SetEditCommands()
        {
            this.IsIndexing = this.indexingService.IsIndexing;

            if (this.EditTracksCommand != null) this.EditTracksCommand.RaiseCanExecuteChanged();
            if (this.RemoveSelectedTracksCommand != null) this.RemoveSelectedTracksCommand.RaiseCanExecuteChanged();
        }
        #endregion

        #region Abstract
        protected abstract void Subscribe();
        protected abstract void Unsubscribe();
        protected abstract Task ShowPlayingTrackAsync();
        protected abstract Task FillListsAsync();
        protected abstract void FilterLists();
        protected abstract void ConditionalScrollToPlayingTrack();
        protected abstract void MetadataService_RatingChangedAsync(RatingChangedEventArgs e);
        protected abstract void MetadataService_LoveChangedAsync(LoveChangedEventArgs e);
        protected abstract void ShowSelectedTrackInformation();
        protected abstract Task LoadedCommandAsync();
        protected abstract void EditSelectedTracks();
        protected abstract void SelectedTracksHandler(object parameter);
        #endregion
    }
}
