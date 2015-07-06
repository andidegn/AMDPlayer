﻿using AMD.util.file;
using AMD.util.media;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;

namespace AMD.AMDPlayer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged {
        #region Constant parameters
        public const int VERSION_MAJOR = 1;
        public const int VERSION_MINOR = 1;
        public const int VERSION_BUILD = 3;
        public const int VERSION_REVISION = 28;

        //public static String Version {
        //	get { return VERSION_MAJOR + "." + VERSION_MINOR + "." + VERSION_BUILD + "-" + VERSION_REVISION; }
        //}

        private Version GetRunningVersion() {
            Version version;
            if (System.Deployment.Application.ApplicationDeployment.IsNetworkDeployed) {
                version = System.Deployment.Application.ApplicationDeployment.CurrentDeployment.CurrentVersion;
            } else {
                version = new Version(VERSION_MAJOR, VERSION_MINOR, VERSION_BUILD, VERSION_REVISION);
            }
            return version;
        }

        private const int CONSTANT_HIDE_COMPONENTS_DELAY = 2;
        private const double CONSTANT_VOLUME_STEP = 0.025;
        private const int CONSTANT_SMALL_STEP_POSITION_S = 5;
        private const int CONSTANT_FRAME_STEP_POSITION_MS = 30;
        private const int DRAGMOVE_TIME_MS = 10;
        #endregion // Constant parameters

        private readonly String TIME_FORMAT = @"hh\.mm\.ss";

        #region notify pattern (NOT FUCKING WORKING!!!! PIECE OF SHIT!!!)
        System.ComponentModel.ICollectionView aView;
        private readonly PropertyChangedEventArgs ARGS_VOLUME = new PropertyChangedEventArgs("Volume");
        private readonly PropertyChangedEventArgs ARGS_VISIBILITY = new PropertyChangedEventArgs("Visibility");

        public event PropertyChangedEventHandler PropertyChanged;
        private void on_PropertyChanged(PropertyChangedEventArgs args) {
            if (PropertyChanged != null) {
                PropertyChanged(this, args);
            }
        }

        private int _height;
        public int CustomHeight {
            get { return _height; }
            set {
                if (value != _height) {
                    _height = value;
                    if (PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("CustomHeight"));
                }
            }
        }
        private int _width;
        public int CustomWidth {
            get { return _width; }
            set {
                if (value != _width) {
                    _width = value;
                    if (PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("CustomWidth"));
                }
            }
        }

        //public double _volume;
        //public double Volume {
        //    get { return _volume; }
        //    set {
        //        _volume = value;
        //        on_PropertyChanged(ARGS_VOLUME);
        //    }
        //}

        Visibility _components_visible;
        Visibility ComponentsVisible {
            get { return _components_visible; }
            set {
                _components_visible = value;
                on_PropertyChanged(ARGS_VISIBILITY);
            }
        }
        #endregion // notify pattern (NOT FUCKING WORKING!!!! PIECE OF SHIT!!!)

        private enum PLAYER_STATE {
            PLAYING,
            PAUSED,
            STOPPED,
            NOT_LOADED
        }

        private enum DURATION_LABEL {
            ELAPSED,
            REMAINING
        }

        private enum MEDIA_TYPE {
            ALL,
            AUDIO,
            AUDIO_AND_VIDEO,
            IMAGE,
            VIDEO
        }

        private ResizeMode _resize_mode_default;
        private PLAYER_STATE _player_state;
        private DURATION_LABEL _progress_state;
        private MEDIA_TYPE _current_media_type;


        private DispatcherTimer _timer;
        private DispatcherTimer _components_visible_timer;
        private DispatcherTimer _timer_dragmove;

        private TimeSpan _temp_position;

        private Brush _ml_main_background;

        private ListViewItem _lv_playlist_active_lvi;

        #region Key First Pressed
        private Key _prev_key_pressed;
        #endregion // Key First Pressed

        #region Playlist
        private Visibility _flag_playlist_visibility_state_window;
        private Stack<MyMediaFile> _playlist_history;
        private MyMediaFile _file_playing;
        private int _item_playing_index = -1;
        private int _playlist_move_item_index = -1;
        private Boolean _playlist_item_clicked = false;
        private readonly String TITLE_LITERATE = "Title";
        private readonly String FILE_PATH_LITERATE = "FilePath.AbsolutePath";
        #endregion // Playlist

		private bool _is_expanded;
		private bool _is_fullscreen;

		#region Slider
		private Boolean _is_slider_dragging;
        #endregion // Slider

        public MainWindow() {
            this.DataContext = this;

            InitializeComponent();
            _load_settings();
            _init_rest();
        }

        #region Settings
        private void _load_settings() {
        }

        private void _save_settings() {
            Properties.Settings.Default.Save();
        }

		private void _exit() {
			if (_is_fullscreen) {
                _fullscreen_leave();
            }
            _save_settings();
            Application.Current.Shutdown(0);
        }
        #endregion // Settings

        private void _init_rest() {
            lbl_title.Content = lbl_title.Content as string + " v" + GetRunningVersion();

			_is_expanded = main_context_view_expanded.IsChecked;
			_is_fullscreen = false;

            _player_state = PLAYER_STATE.NOT_LOADED;
            _progress_state = DURATION_LABEL.ELAPSED;
            _current_media_type = MEDIA_TYPE.VIDEO;

            _ml_main_background = border_media_element.Background;

            _resize_mode_default = this.ResizeMode;

            _temp_position = TimeSpan.MinValue;

            _playlist_history = new Stack<MyMediaFile>(100);


            _timer = new DispatcherTimer();
            _timer.Interval = new TimeSpan(0, 0, 1);
            _timer.Tick += new EventHandler((sender, e) => {
                if (!_is_slider_dragging && _player_state == PLAYER_STATE.PLAYING) {
                    sl_progress.Value = ml_main.Position.TotalSeconds;
                    this.TaskbarItemInfo.ProgressValue = ml_main.NaturalDuration.HasTimeSpan ? sl_progress.Value / ml_main.NaturalDuration.TimeSpan.TotalSeconds : 0;
                }
            });
            _timer.Start();

            _components_visible_timer = new DispatcherTimer();
            _components_visible_timer.Interval = new TimeSpan(0, 0, CONSTANT_HIDE_COMPONENTS_DELAY);
            _components_visible_timer.Tick += new EventHandler((sender, e) => {
				if (_is_expanded || _is_fullscreen && _player_state == PLAYER_STATE.PLAYING && !main_context.IsOpen) {
                    _components_state(Visibility.Hidden);
                }
            });
            _components_visible_timer.Start();

            _timer_dragmove = new DispatcherTimer();
            _timer_dragmove.Interval = new TimeSpan(0, 0, 0, 0, DRAGMOVE_TIME_MS);
            _timer_dragmove.Tick += new EventHandler((t_sender, t_e) => {
                if (Mouse.LeftButton == MouseButtonState.Pressed) {
                    DragMove();
                }
                _timer_dragmove.Stop();
            });

            /* Subscribe to custome eventhandler for external application launch */
            App.ExternelApplicationLauncy += mainwindow_root_loaded;
        }

        #region GUI update
        private void _update_media_position() {
            ml_main.Position = new TimeSpan(0, 0, (int)sl_progress.Value);
            _lbl_progress_update();
        }

        private void _lbl_progress_update() {
            switch (_progress_state) {
                case DURATION_LABEL.ELAPSED:
                    lbl_progress.Content = ml_main.Position.ToString(TIME_FORMAT);
                    break;
                case DURATION_LABEL.REMAINING:
                    if (ml_main.NaturalDuration.HasTimeSpan) {
                        lbl_progress.Content = (ml_main.NaturalDuration.TimeSpan - ml_main.Position).ToString(TIME_FORMAT);
                    }
                    break;
                default:
                    break;
            }
        }

        private void _components_state(Visibility state) {
            if (!Dispatcher.Thread.Equals(Thread.CurrentThread)) {
                Dispatcher.Invoke(() => _components_state(state));
                return;
            }
            component_state_flag.Visibility = state;
            this.Cursor = null;
            if (state != Visibility.Visible) {
				if (_is_fullscreen) {
                    this.Cursor = Cursors.None;
                }
            }
        }

        private void _resize_mode_toggle() {
            if (this.ResizeMode == _resize_mode_default) {
                this.ResizeMode = ResizeMode.CanResizeWithGrip;
            } else {
                this.ResizeMode = _resize_mode_default;
            }
            if (ml_main.Position.TotalMilliseconds != 0) {
                _temp_position = ml_main.Position;
            }
        }

        private void _volume_update(double sign) {
            sl_volume.Value += sign * CONSTANT_VOLUME_STEP * (_is_ctrl_down() ? 10 : 1);
        }

        private void _lv_playlist_set_active_color(int index) {
            if (lv_playlist.Items.Count < 1) {
                return;
            }
            if (_lv_playlist_active_lvi != null) {
                _lv_playlist_active_lvi.Foreground = new SolidColorBrush(Colors.Black);
            }
            _lv_playlist_active_lvi = (lv_playlist.ItemContainerGenerator.ContainerFromIndex(index) as ListViewItem);
            lv_playlist.Foreground = new SolidColorBrush(Colors.Black);
            if (_lv_playlist_active_lvi != null) {
                _lv_playlist_active_lvi.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
        #endregion // GUI update

        #region Screen State
        private void _fullwindow_toggle() {
            mainwindow_root.Activate();
			if (!_is_fullscreen) {
                if (_player_state <= PLAYER_STATE.PAUSED) {
					_fullscreen_enter();
					main_context_view_fullscreen.IsChecked = true;
                }
            } else {
				_fullscreen_leave();
				main_context_view_fullscreen.IsChecked = false;
            }
        }

        private void _expand_toggle() {
            mainwindow_root.Activate();
			if (!_is_fullscreen) {
				if (!_is_expanded) {
                    _expand();
					main_context_view_expanded.IsChecked = true;
                } else {
					_contract();
					main_context_view_expanded.IsChecked = false;
                }
            }
        }

		private void _fullscreen_enter() {
            if (ResizeMode == ResizeMode.CanResizeWithGrip) {
                _resize_mode_toggle();
            }
            // Playlist
            if ((_flag_playlist_visibility_state_window = lv_playlist.Visibility) == Visibility.Visible) {
                _playlist_toggle();
            }
			// Expanded
			if (!_is_expanded) {
				_expand();
			}
            // WindowState
			this.WindowState = WindowState.Maximized;
			_is_fullscreen = true;
        }

        private void _fullscreen_leave() {
            // Playlist
            if (_flag_playlist_visibility_state_window != lv_playlist.Visibility) {
                _playlist_toggle();
            }
			// Expanded
			if (!main_context_view_expanded.IsChecked) {
				_contract();
			}
            // WindowState
            this.WindowState = WindowState.Normal;
            //if (_resize_enabled) {
            //    _resize_mode_toggle();
			//}
			_is_fullscreen = false;
        }

        private void _expand() {
            // Border
            this.border_main.BorderThickness = new Thickness(0);
            this.border_main.Background = Brushes.Black;

            // Components
			component_state_flag.Background = new SolidColorBrush(Colors.Transparent);
            Grid.SetRowSpan(grid_media, 2);

            //Properties.Settings.Default.width -= lv_playlist.Width;
            //Properties.Settings.Default.height -= grid_bottom_controls.Height;
            //CustomWidth = (int)(this.ActualWidth - lv_playlist.Width);
            //CustomHeight = (int)(this.ActualHeight - grid_bottom_controls.Height);
            //UpdateLayout();


            //    Height = "{Binding height, Source={x:Static Properties:Settings.Default}, Mode=TwoWay}"
            //Width = "{Binding width, Source={x:Static Properties:Settings.Default}, Mode=TwoWay}"
			_is_expanded = true;

			try {
				_components_visible_timer.Start();
			} catch {}
        }

        private void _contract() {

            // Border
            //this.border_main.Margin = _border_margin;
            this.border_main.Background = Brushes.White;
            this.border_main.BorderThickness = new Thickness(1);

            // Components
			_components_state(Visibility.Visible);
			component_state_flag.Background = new SolidColorBrush(Colors.White);
            Grid.SetRowSpan(grid_media, 1);

            //Properties.Settings.Default.width += lv_playlist.Width;
            //Properties.Settings.Default.height += grid_bottom_controls.Height;
            //CustomWidth = (int)(this.ActualWidth + lv_playlist.Width);
            //CustomHeight = (int)(this.ActualHeight + grid_bottom_controls.Height);
            //UpdateLayout();
			_is_expanded = false;
        }

        private void _always_on_top() {
            if (main_context_view_always_on_top.IsChecked) {
                main_context_view_always_on_top_when_playing.IsChecked = false;
            }
        }

        private void _always_on_top_when_playing() {
            if (main_context_view_always_on_top_when_playing.IsChecked) {
                main_context_view_always_on_top.IsChecked = false;
                if (_player_state == PLAYER_STATE.PLAYING) {
                    this.Topmost = true;
                } else {
                    this.Topmost = false;
                }
            } else {
                this.Topmost = false;
            }
        }
        #endregion // Screen State

        #region File handeling
        private async void _open_files(Boolean clear_playlist_and_start_playback, MEDIA_TYPE media_type, params String[] filenames) {
            /* Bring window to front. This is relevant when opening via file association */
            if (!mainwindow_root.IsActive) {
                mainwindow_root.Activate();
            }
            if (filenames != null) {
                Task<String[]> get_files_task;
                switch (media_type) {
                    case MEDIA_TYPE.ALL:
                        get_files_task = new Task<String[]>(() => _get_all_mediafiles(filenames));
                        break;

                    case MEDIA_TYPE.AUDIO:
                        get_files_task = new Task<String[]>(() => _get_all_audiofiles(filenames));
                        break;

                    case MEDIA_TYPE.AUDIO_AND_VIDEO:
                        get_files_task = new Task<String[]>(() => _get_all_avfiles(filenames));
                        break;

                    case MEDIA_TYPE.IMAGE:
                        get_files_task = new Task<String[]>(() => _get_all_imagefiles(filenames));
                        break;

                    case MEDIA_TYPE.VIDEO:
                        get_files_task = new Task<String[]>(() => _get_all_videofiles(filenames));
                        break;

                    default:
                        get_files_task = new Task<String[]>(() => _get_all_videofiles(filenames));
                        break;
                }
                get_files_task.Start();
                await get_files_task;
                filenames = get_files_task.Result;
                _current_media_type = media_type;

                if (filenames.Length < 1) {
                    MessageBox.Show("No playable media found", "Error", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                } else {
                    if (clear_playlist_and_start_playback) {
                        _playlist_clear();
                    }
                    Task populate = new Task(() => _playlist_populate(filenames));
                    populate.Start();
                    await populate;

                    if (clear_playlist_and_start_playback) {
                        _play(0);
                    } else {
                        _playlist_update(0);
                    }
                }
            }
        }

        private String[] _get_all_imagefiles(params String[] filenames) {
            HashSet<String> set = new HashSet<string>();
            FileHandling.get_files_in_directory(set, filenames);
            return Array.FindAll<String>(set.ToArray<String>(), FileHandling.is_image_file);
        }

        private String[] _get_all_audiofiles(params String[] filenames) {
            HashSet<String> set = new HashSet<string>();
            FileHandling.get_files_in_directory(set, filenames);
            return Array.FindAll<String>(set.ToArray<String>(), FileHandling.is_audio_file);
        }

        private String[] _get_all_videofiles(params String[] filenames) {
            HashSet<String> set = new HashSet<string>();
            FileHandling.get_files_in_directory(set, filenames);
            return Array.FindAll<String>(set.ToArray<String>(), FileHandling.is_video_file);
        }

        private String[] _get_all_mediafiles(params String[] filenames) {
            HashSet<String> set = new HashSet<string>();
            FileHandling.get_files_in_directory(set, filenames);
            return Array.FindAll<String>(set.ToArray<String>(), FileHandling.is_media_file);
        }

        private String[] _get_all_avfiles(params String[] filenames) {
            HashSet<String> set = new HashSet<string>();
            FileHandling.get_files_in_directory(set, filenames);
            return Array.FindAll<String>(set.ToArray<String>(), delegate(string s) { return FileHandling.is_audio_file(s) || FileHandling.is_video_file(s); });
        }
        #endregion // File handling

        #region Playback
        private int _random_playlist_entry() {
            Random r = new Random();
            int next_entry;
            do {
                next_entry = r.Next(lv_playlist.Items.Count);
            } while (next_entry == lv_playlist.SelectedIndex);
            return next_entry;
        }

        private void _play(Boolean play_pause) {
            //if (!play_pause && _player_state == PLAYER_STATE.PLAYING) {
            //    _stop();
            //}
            switch (_player_state) {
                case PLAYER_STATE.PAUSED:
                    _playback_start();
                    break;
                case PLAYER_STATE.PLAYING:
                    _pause();
                    break;
                case PLAYER_STATE.STOPPED:
                case PLAYER_STATE.NOT_LOADED:
                default:
                    _play(lv_playlist.SelectedIndex);
                    break;
            }
        }

        private void _play(int index) {
            index = index < 0 && lv_playlist.Items.Count > 0 ? 0 : index;
            if (index > -1 && index < lv_playlist.Items.Count) {
                _playlist_update(index);
                _file_playing = lv_playlist.SelectedItem as MyMediaFile;
                _play();
            }
        }

        private void _play() {
            lv_playlist.ScrollIntoView(_file_playing);
            Uri uri = _file_playing.FilePath;
            lbl_title.Content = uri.LocalPath;
            this.Title = Path.GetFileName(uri.LocalPath);
            ml_main.Close();
            ml_main.Source = uri;
            JumpList.AddToRecentCategory(Uri.UnescapeDataString(uri.LocalPath));
            JumpList.GetJumpList(App.Current).Apply();
            _lv_playlist_set_active_color(_item_playing_index);
            _playback_start();
        }

        private void _playback_start() {
            _player_state = PLAYER_STATE.PLAYING;
            btn_play.Background = FindResource("Pause") as DrawingBrush;
            ml_main.Volume = sl_volume.Value;
            ml_main.Play();
            if (main_context_view_always_on_top_when_playing.IsChecked) {
                this.Topmost = true;
            }
            if (border_media_element.Background == _ml_main_background) {
                border_media_element.Background = new SolidColorBrush(Colors.Black);
            }
            this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
        }

        private void _stop() {
            btn_play.Background = FindResource("Play_fwd") as DrawingBrush;
			if (_is_fullscreen) {
                _fullwindow_toggle();
            }
            ml_main.Stop();
            ml_main.Source = null;
            border_media_element.Background = _ml_main_background;
            if (main_context_view_always_on_top_when_playing.IsChecked) {
                this.Topmost = false;
            }
            this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
            _player_state = PLAYER_STATE.STOPPED;
        }

        private void _pause() {
            btn_play.Background = FindResource("Play_fwd") as DrawingBrush;
            ml_main.Pause();
            if (main_context_view_always_on_top_when_playing.IsChecked) {
                this.Topmost = false;
            }
            this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
            _player_state = PLAYER_STATE.PAUSED;
        }

        private void _prev() {
            if (ml_main.Position.TotalSeconds > 2) {
                ml_main.Position = new TimeSpan(0, 0, 0);
            } else {
                if (lv_playlist.Items.Count < 2) {
                    if (_file_playing != null) {
                        String filename = FileHandling.get_file_relative_to(_file_playing.FilePath.LocalPath, -1);
                        if (filename != null) {
                            _open_files(true, _current_media_type, filename);
                        }
                    }
                } else {
                    if (btn_shuffle.IsChecked == true && _playlist_history.Count > 0) {
                        _file_playing = _playlist_history.Pop();
                        for (int i = 0; i < lv_playlist.Items.Count; i++) {
                            if ((lv_playlist.Items[i] as MyMediaFile).Equals(_file_playing)) {
                                _playlist_update(i);
                            }
                        }
                        _play();
                    } else if (_item_playing_index > -1) {
                        _play(--_item_playing_index);
                    }
                }
            }
        }

        private void _next(Boolean media_ended) {
            switch (lv_playlist.Items.Count) {
                case 0:
                case 1:
                    if (_file_playing == null) {
                        break;
                    }
                    if (btn_auto_play_next_in_folder.IsChecked == true || !media_ended) {
                        String filename = FileHandling.get_file_relative_to(_file_playing.FilePath.LocalPath, 1);
                        if (filename != null) {
                            _open_files(true, _current_media_type, filename);
                            break;
                        }
                    }
                    _stop();
                    break;

                default:
                    _playlist_history.Push(_file_playing);
                    if (btn_shuffle.IsChecked == true) {
                        _play(_random_playlist_entry());
                    } else if (_item_playing_index < lv_playlist.Items.Count - 1) {
                        _play(++_item_playing_index);
                    }
                    break;
            }
        }
        #endregion // Playback

        #region View
        private void _aspect_normal() {
            main_context_aspect_zoom.IsChecked = main_context_aspect_stretch.IsChecked = false;
            ml_main.Stretch = Stretch.Uniform;
        }

        private void _aspect_zoom() {
            main_context_aspect_normal.IsChecked = main_context_aspect_stretch.IsChecked = false;
            ml_main.Stretch = Stretch.UniformToFill;
        }

        private void _aspect_stretch() {
            main_context_aspect_normal.IsChecked = main_context_aspect_zoom.IsChecked = false;
            ml_main.Stretch = Stretch.Fill;
        }
        #endregion // View

        #region Playlist
        private Boolean _playlist_has_focus() {
            return FocusManager.GetFocusedElement(this) is ListView || FocusManager.GetFocusedElement(this) is ListViewItem;
        }

        private void _playlist_populate(String[] filenames) {
            if (!Dispatcher.Thread.Equals(Thread.CurrentThread)) {
                Dispatcher.Invoke(() => _playlist_populate(filenames));
                return;
            }
            foreach (String file in filenames) {
                Uri path = new Uri(file);
                String filename = Path.GetFileNameWithoutExtension(path.LocalPath);
                lv_playlist.Items.Add(new MyMediaFile(filename, path));
            }
        }

        private void _playlist_update(int index) {
            lv_playlist.SelectedIndex = _item_playing_index = index;
        }

        private void _playlist_show() {
            // Components

            lv_playlist.Visibility = Visibility.Visible;
            Grid.SetColumnSpan(border_media_element, 1);
            Grid.SetColumnSpan(lbl_title, 1);
        }

        private void _playlist_hide() {
            // Components

            lv_playlist.Visibility = Visibility.Hidden;
            Grid.SetColumnSpan(border_media_element, grid_media.ColumnDefinitions.Count);
            Grid.SetColumnSpan(lbl_title, grid_media.ColumnDefinitions.Count);
        }

        private void _playlist_toggle() {
            if (lv_playlist.Visibility == Visibility.Visible) {
                _playlist_hide();
            } else {
                _flag_playlist_visibility_state_window = Visibility.Visible;
                _playlist_show();
            }
        }

        private void _playlist_delete_at(int index) {
            if (_playlist_has_focus()) {
                Boolean __is_last_item = index == lv_playlist.Items.Count - 1;
                lv_playlist.Items.RemoveAt(index);
                lv_playlist.SelectedIndex = __is_last_item ? index - 1 : index;
                lv_playlist.Focus();

                if (_item_playing_index > 0 && (index < _item_playing_index || (index == _item_playing_index && __is_last_item))) {
                    _item_playing_index--;
                }
                if (lv_playlist.Items.Count < 1) {
                    _stop();
                }
            }
        }

        public void _playlist_move_up() {
            _playlist_move(-1);
        }

        public void _playlist_move_down() {
            _playlist_move(1);
        }

        public void _playlist_move(int direction) {
            // Checking selected item
            if (lv_playlist.SelectedItem == null || lv_playlist.SelectedIndex < 0)
                return; // No selected item - nothing to do

            // Calculate new index using move direction
            int newIndex = lv_playlist.SelectedIndex + direction;

            // Checking bounds of the range
            if (newIndex < 0 || newIndex >= lv_playlist.Items.Count)
                return; // Index out of range - nothing to do

            MyMediaFile selected = lv_playlist.SelectedItem as MyMediaFile;

            // Removing removable element
            lv_playlist.Items.Remove(selected);
            // Insert it in new position
            lv_playlist.Items.Insert(newIndex, selected);
            // Restore selection
            lv_playlist.SelectedIndex = newIndex;
        }

        private void _playlist_sort(String property_name) {
            SortDescription sd;
            if (lv_playlist.Items.SortDescriptions.Count > 0) {
                sd = new SortDescription(lv_playlist.Items.SortDescriptions[0].PropertyName, lv_playlist.Items.SortDescriptions[0].Direction);
            } else {
                sd = new SortDescription();
            }
            if (sd.PropertyName == property_name) {
                sd.Direction = sd.Direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            } else {
                sd.PropertyName = property_name;
                sd.Direction = ListSortDirection.Ascending;
            }
            lv_playlist.Items.SortDescriptions.Clear();
            lv_playlist.Items.SortDescriptions.Add(sd);
        }

        private void _playlist_clear() {
            lv_playlist.Items.Clear();
            _item_playing_index = -1;
            //if (lv_playlist.Items.Count < 1) {
            //    _stop();
            //}
        }
        #endregion // Playlist

        #region Keyboard util
        private Boolean _is_key_down(Key key) {
            return Keyboard.IsKeyDown(key);
        }

        private Boolean _is_shift_down() {
            return _is_key_down(Key.LeftShift) || _is_key_down(Key.RightShift);
        }

        private Boolean _is_ctrl_down() {
            return _is_key_down(Key.LeftCtrl) || _is_key_down(Key.RightCtrl);
        }

        private Boolean _is_alt_down() {
            return _is_key_down(Key.LeftAlt) || _is_key_down(Key.RightAlt);
        }

        private MEDIA_TYPE _get_media_type() {
            MEDIA_TYPE mt = MEDIA_TYPE.AUDIO_AND_VIDEO;
            if (_is_key_down(Key.A)) {
                mt = MEDIA_TYPE.ALL;
            } else if (_is_key_down(Key.V)) {
                mt = MEDIA_TYPE.VIDEO;
            } else if (_is_key_down(Key.M)) {
                mt = MEDIA_TYPE.AUDIO;
            } else if (_is_key_down(Key.I)) {
                mt = MEDIA_TYPE.IMAGE;
            }
            return mt;
        }
        #endregion // Keyboard util

        #region eventhandlers
        #region Buttons
        private void btn_open_Click(object sender, RoutedEventArgs e) {
            Boolean clear_playlist = !_is_ctrl_down();
            OpenFileDialog ofd = FileHandling.MediaOpenFileDialog();
            if (ofd.ShowDialog() == true) {
                _open_files(clear_playlist, MEDIA_TYPE.ALL, ofd.FileNames);
            }
        }

        private void btn_play_Click(object sender, RoutedEventArgs e) {
            _play(false);
        }

        private void btn_stop_Click(object sender, RoutedEventArgs e) {
            _stop();
        }

        private void btn_prev_Click(object sender, RoutedEventArgs e) {
            _prev();
        }

        private void btn_next_Click(object sender, RoutedEventArgs e) {
            _next(false);
        }

        private void btn_playlist_toggle_Click(object sender, RoutedEventArgs e) {
            _playlist_toggle();
        }

        private void btn_exit_Click(object sender, RoutedEventArgs e) {
            _exit();
        }
        #endregion // Buttons

        #region Labels
        private void lbl_progress_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            _progress_state = _progress_state == DURATION_LABEL.ELAPSED ? DURATION_LABEL.REMAINING : DURATION_LABEL.ELAPSED;
            _lbl_progress_update();
        }
        #endregion // Labels

        #region ListView playlist
        #region ListViewItem
        private void lvi_DoubleClick(object sender, MouseButtonEventArgs e) {
            _item_playing_index = lv_playlist.SelectedIndex;
            _play(lv_playlist.SelectedIndex);
            e.Handled = true;
        }
        #endregion // ListViewItem

        private void lv_playlist_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                String[] _files = (String[])e.Data.GetData(DataFormats.FileDrop);
                _open_files(false, _get_media_type(), _files);
            }
        }

        private void lv_playlist_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_playlist_move_item_index != -1) {
                lv_playlist.SelectionChanged -= lv_playlist_SelectionChanged;
                int direction = lv_playlist.SelectedIndex - _playlist_move_item_index;
                if (direction < 0) {
                    lv_playlist.SelectedIndex++;
                    _playlist_move_up();
                } else if (direction > 0) {
                    lv_playlist.SelectedIndex--;
                    _playlist_move_down();
                }
                lv_playlist.SelectionChanged += lv_playlist_SelectionChanged;
            }
            if (_playlist_item_clicked) {
                _playlist_move_item_index = lv_playlist.SelectedIndex;
            }
        }

        private void lv_playlist_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            _playlist_item_clicked = true;
        }

        private void lv_playlist_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            _playlist_move_item_index = -1;
            _playlist_item_clicked = false;
        }

        private void lv_playlist_KeyDown(object sender, KeyEventArgs e) {
            e.Handled = true;
        }

        #region ListView playlist Context menu
        private void lv_context_clear_playlist_Click(object sender, RoutedEventArgs e) {
            _playlist_clear();
        }

        private void lv_playlist_context_sort_by_lable_Click(object sender, RoutedEventArgs e) {
            _playlist_sort(TITLE_LITERATE);
        }

        private void lv_playlist_context_sort_by_path_Click(object sender, RoutedEventArgs e) {
            _playlist_sort(FILE_PATH_LITERATE);
        }
        #endregion // ListView playlist Context menu
        #endregion // Listview playlist

        #region Mainwindow
        private void mainwindow_toggle_fullscreen(object sender, MouseButtonEventArgs e) {
            if (Mouse.DirectlyOver == border_media_element || Mouse.DirectlyOver == ml_main) {
                if (_is_ctrl_down()) {
                    _expand_toggle();
                } else {
                    _fullwindow_toggle();
                }
            }
        }

        private void mainwindow_root_loaded(object sender, EventArgs e) {
            if (Application.Current.Properties["FileName"] != null) {
                string __filename = Application.Current.Properties["FileName"].ToString();
                _open_files(_is_ctrl_down() ? false : true, MEDIA_TYPE.ALL, __filename);
            }
        }

        private void mainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            _timer_dragmove.Start();
        }

        private void mainwindow_root_show_components(object sender, MouseEventArgs e) {
			if (_is_expanded) {
                _components_visible_timer.Stop();
                _components_state(Visibility.Visible);
                if (e.LeftButton == MouseButtonState.Released || Mouse.DirectlyOver != mainwindow_root) {
                    _components_visible_timer.Start();
                }
            }
        }

        private void mainwindow_root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			if (_is_expanded) {
                _components_visible_timer.Start();
            }
        }

        private void mainwindow_root_KeyDown(object sender, KeyEventArgs e) {
            //e.Key == Key.MediaNextTrack
            e.Handled = true;
            switch (e.Key) {
                case Key.A:
                    if (_prev_key_pressed == Key.T) {
                        main_context_view_always_on_top.IsChecked = !main_context_view_always_on_top.IsChecked;
                    }
                    break;
                case Key.AbntC1:
                    break;
                case Key.AbntC2:
                    break;
                case Key.Add:
                    break;
                case Key.Apps:
                    break;
                case Key.Attn:
                    break;
                case Key.B:
                    break;
                case Key.Back:
                    break;
                case Key.BrowserBack:
                    break;
                case Key.BrowserFavorites:
                    break;
                case Key.BrowserForward:
                    break;
                case Key.BrowserHome:
                    break;
                case Key.BrowserRefresh:
                    break;
                case Key.BrowserSearch:
                    break;
                case Key.BrowserStop:
                    break;
                case Key.C:
                    break;
                case Key.Cancel:
                    break;
                //case Key.Capital:
                //    break;
                case Key.CapsLock:
                    break;
                case Key.Clear:
                    break;
                case Key.CrSel:
                    break;
                case Key.D:
                    break;
                case Key.D0:
                    break;
                case Key.D1:
                    if (_is_ctrl_down()) {
                        _aspect_normal();
                    }
                    break;
                case Key.D2:
                    if (_is_ctrl_down()) {
                        _aspect_zoom();
                    }
                    break;
                case Key.D3:
                    if (_is_ctrl_down()) {
                        _aspect_stretch();
                    }
                    break;
                case Key.D4:
                    break;
                case Key.D5:
                    break;
                case Key.D6:
                    break;
                case Key.D7:
                    if (_is_ctrl_down()) {
                        _playlist_toggle();
                    }
                    break;
                case Key.D8:
                    break;
                case Key.D9:
                    break;
                //case Key.DbeAlphanumeric:
                //    break;
                //case Key.DbeCodeInput:
                //    break;
                //case Key.DbeDbcsChar:
                //    break;
                //case Key.DbeDetermineString:
                //    break;
                //case Key.DbeEnterDialogConversionMode:
                //    break;
                //case Key.DbeEnterImeConfigureMode:
                //    break;
                //case Key.DbeEnterWordRegisterMode:
                //    break;
                case Key.DbeFlushString:
                    break;
                case Key.DbeHiragana:
                    break;
                case Key.DbeKatakana:
                    break;
                case Key.DbeNoCodeInput:
                    break;
                //case Key.DbeNoRoman:
                //    break;
                case Key.DbeRoman:
                    break;
                case Key.DbeSbcsChar:
                    break;
                case Key.DeadCharProcessed:
                    break;
                case Key.Decimal:
                    break;
                case Key.Delete:
                    if (lv_playlist.Visibility == Visibility.Visible && lv_playlist.SelectedIndex > -1) {

                        _playlist_delete_at(lv_playlist.SelectedIndex);
                    }
                    break;
                case Key.Divide:
                    break;
                case Key.Down:
                    if (!_playlist_has_focus()) {
                        _volume_update(-1);
                    } else if (_is_ctrl_down()) {
                        _playlist_move_down();
                    } else {
                        e.Handled = false;
                    }
                    break;
                case Key.E:
                    break;
                case Key.End:
                    break;
                case Key.Enter:
                    break;
                //case Key.EraseEof:
                //break;
                case Key.Escape:
                    break;
                //case Key.ExSel:
                //    break;
                case Key.Execute:
                    break;
                case Key.F:
                    if (_is_ctrl_down()) {
                        _expand_toggle();
                    } else {
                        _fullwindow_toggle();
                    }
                    break;
                case Key.F1:
                    break;
                case Key.F10:
                    break;
                case Key.F11:
                    break;
                case Key.F12:
                    break;
                case Key.F13:
                    break;
                case Key.F14:
                    break;
                case Key.F15:
                    break;
                case Key.F16:
                    break;
                case Key.F17:
                    break;
                case Key.F18:
                    break;
                case Key.F19:
                    break;
                case Key.F2:
                    break;
                case Key.F20:
                    break;
                case Key.F21:
                    break;
                case Key.F22:
                    break;
                case Key.F23:
                    break;
                case Key.F24:
                    break;
                case Key.F3:
                    break;
                case Key.F4:
                    break;
                case Key.F5:
                    break;
                case Key.F6:
                    break;
                case Key.F7:
                    break;
                case Key.F8:
                    break;
                case Key.F9:
                    break;
                case Key.FinalMode:
                    break;
                case Key.G:
                    break;
                case Key.H:
                    _expand_toggle();
                    break;
                case Key.HangulMode:
                    break;
                case Key.HanjaMode:
                    break;
                case Key.Help:
                    break;
                case Key.Home:
                    break;
                case Key.I:
                    break;
                case Key.ImeAccept:
                    break;
                case Key.ImeConvert:
                    break;
                case Key.ImeModeChange:
                    break;
                case Key.ImeNonConvert:
                    break;
                case Key.ImeProcessed:
                    break;
                case Key.Insert:
                    break;
                case Key.J:
                    break;
                case Key.JunjaMode:
                    break;
                case Key.K:
                    break;
                //case Key.KanaMode:
                //    break;
                //case Key.KanjiMode:
                //    break;
                case Key.L:
                    break;
                case Key.LWin:
                    break;
                case Key.LaunchApplication1:
                    break;
                case Key.LaunchApplication2:
                    break;
                case Key.LaunchMail:
                    break;
                case Key.Left:
                    ml_main.Position = ml_main.Position.Subtract(new TimeSpan(0, 0, CONSTANT_SMALL_STEP_POSITION_S));
                    break;
                case Key.LeftAlt:
                    break;
                case Key.LeftCtrl:
                    break;
                case Key.LeftShift:
                    break;
                case Key.LineFeed:
                    break;
                case Key.M:
                    break;
                /* Media buttons */
                case Key.MediaPlayPause:
                    _play(true);
                    break;
                case Key.MediaStop:
                    _stop();
                    break;
                case Key.MediaPreviousTrack:
                    _prev();
                    break;
                case Key.MediaNextTrack:
                    _next(false);
                    break;
                case Key.Multiply:
                    break;
                case Key.N:
                    break;
                case Key.None:
                    break;
                case Key.NumLock:
                    break;
                case Key.NumPad0:
                    break;
                case Key.NumPad1:
                    break;
                case Key.NumPad2:
                    break;
                case Key.NumPad3:
                    break;
                case Key.NumPad4:
                    break;
                case Key.NumPad5:
                    break;
                case Key.NumPad6:
                    break;
                case Key.NumPad7:
                    break;
                case Key.NumPad8:
                    break;
                case Key.NumPad9:
                    break;
                case Key.O:
                    break;
                case Key.P:
                    if (_prev_key_pressed == Key.T) {
                        main_context_view_always_on_top_when_playing.IsChecked = !main_context_view_always_on_top_when_playing.IsChecked;
                    }
                    break;
                case Key.PageDown:
                    break;
                case Key.PageUp:
                    break;
                case Key.Pause:
                    break;
                case Key.Print:
                    break;
                case Key.PrintScreen:
                    break;
                case Key.Q:
                    break;
                case Key.R:
					if (!_is_fullscreen) {
                        //if (this.ResizeMode == _resize_mode_default) {
                        _resize_mode_toggle();
                        //}

                    }
                    break;
                case Key.RWin:
                    break;
                case Key.Right:
                    ml_main.Position = ml_main.Position.Add(new TimeSpan(0, 0, CONSTANT_SMALL_STEP_POSITION_S));
                    break;
                case Key.RightAlt:
                    break;
                case Key.RightCtrl:
                    break;
                case Key.RightShift:
                    break;
                case Key.S:
                    break;
                case Key.Scroll:
                    break;
                case Key.Select:
                    break;
                case Key.SelectMedia:
                    break;
                case Key.Separator:
                    break;
                case Key.Sleep:
                    break;
                case Key.Space:
                    _play(true);
                    break;
                case Key.Subtract:
                    break;
                case Key.System:
                    break;
                case Key.T:
                    if (_is_ctrl_down()) {
                    }
                    break;
                case Key.Tab:
                    break;
                case Key.U:
                    break;
                case Key.Up:
                    if (!_playlist_has_focus()) {
                        _volume_update(1);
                    } else if (_is_ctrl_down()) {
                        _playlist_move_up();
                    } else {
                        e.Handled = false;
                    }
                    break;
                case Key.V:
                    break;
                case Key.VolumeDown:
                    break;
                case Key.VolumeMute:
                    break;
                case Key.VolumeUp:
                    break;
                case Key.W:
                    break;
                case Key.X:
                    _exit();
                    break;
                case Key.Y:
                    break;
                case Key.Z:
                    break;
                default:
                    e.Handled = false;
                    break;
            }
        }

        private void mainwindow_root_KeyUp(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.R:
                    //if (this.ResizeMode == ResizeMode.CanResizeWithGrip) {
                    //    _resize_mode_toggle();
                    //}

                    break;
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftAlt:
                case Key.RightAlt:
                    _prev_key_pressed = Key.None;
                    break;
                default:
                    break;
            }
            if (_is_ctrl_down() || _is_shift_down() || _is_alt_down()) {
                _prev_key_pressed = e.Key;
            }
        }

        private void mainwindow_root_MouseWheel(object sender, MouseWheelEventArgs e) {
            _volume_update(e.Delta / 240.0);
        }

        private void mainwindow_root_Closing(object sender, CancelEventArgs e) {
            _exit();
        }

        #region Mainwindow Context Menu
        private void main_context_aspect_normal_Click(object sender, RoutedEventArgs e) {
            _aspect_normal();
        }

        private void main_context_aspect_zoom_Click(object sender, RoutedEventArgs e) {
            _aspect_zoom();
        }

        private void main_context_aspect_stretch_Click(object sender, RoutedEventArgs e) {
            _aspect_stretch();
        }

        private void main_context_appearence_gb_colour_Click(object sender, RoutedEventArgs e) {
            // ToDo Implement ColorPicker: http://blogs.msdn.com/b/wpfsdk/archive/2006/10/26/uncommon-dialogs--font-chooser-and-color-picker-dialogs.aspx
            component_state_flag.Background = new SolidColorBrush(Colors.WhiteSmoke);
            NOT_IMPLEMENTED_YET();
        }

        private void main_context_appearence_txt_colour_Click(object sender, RoutedEventArgs e) {
            // ToDo Implement ColorPicker: http://blogs.msdn.com/b/wpfsdk/archive/2006/10/26/uncommon-dialogs--font-chooser-and-color-picker-dialogs.aspx
            component_state_flag.Background = new SolidColorBrush(Colors.White);
            NOT_IMPLEMENTED_YET();
        }

        private void main_context_view_always_on_top_Changed(object sender, RoutedEventArgs e) {
            _always_on_top();
        }

        private void main_context_view_always_on_top_when_playing_Changed(object sender, RoutedEventArgs e) {
            _always_on_top_when_playing();
        }
        #endregion // Mainwindow Context Menu
        #endregion // Mainwindow

        #region MediaElement main
        private void ml_main_MouseDown(object sender, MouseButtonEventArgs e) {
            FocusManager.SetFocusedElement(this, ml_main);
        }

        private void ml_main_MediaOpened(object sender, RoutedEventArgs e) {
            if (ml_main.NaturalDuration.HasTimeSpan) {
                sl_progress.Maximum = ml_main.NaturalDuration.TimeSpan.TotalSeconds;
                lbl_duration.Content = ml_main.NaturalDuration.TimeSpan.ToString(TIME_FORMAT);
                if (_temp_position != TimeSpan.MinValue) {
                    sl_progress.Value = _temp_position.TotalMilliseconds;
                    ml_main.Position = _temp_position;
                    _temp_position = TimeSpan.MinValue;
                }
            }
        }

        private void ml_main_MediaFailed(object sender, ExceptionRoutedEventArgs e) {
            lv_playlist.Items.Remove(lv_playlist.SelectedItem);
            MessageBox.Show("Media loading unsuccessful. " + e.ErrorException.Message);
        }

        void ml_main_MediaEnded(object sender, RoutedEventArgs e) {
            _next(true);
        }

        private void ml_main_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                String[] files = (String[])e.Data.GetData(DataFormats.FileDrop);
                if (_is_key_down(Key.M) && _is_key_down(Key.D) && _is_key_down(Key.D5)) {
                    using (var md5 = System.Security.Cryptography.MD5.Create()) {
                        using (var stream = System.IO.File.OpenRead(files[0])) {
                            MessageBox.Show("The MD5 checksum is: " + BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", ""));
                        }
                    }
                } else {
                    _open_files(true, _get_media_type(), files);
                }
            }
        }
        #endregion // MediaElement main

        #region Sliders
        #region Slider Progress
        private void sl_progress_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            _is_slider_dragging = true;
        }

        private void sl_progress_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            _timer.Start();
            _is_slider_dragging = false;
        }

        private void sl_progress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_is_slider_dragging) {
                _update_media_position();
            } else {
                _lbl_progress_update();
            }
        }

        private void grid_sl_progress_MouseMove(object sender, MouseEventArgs e) {
            Track __track = sl_progress.Template.FindName("PART_Track", sl_progress) as Track;
            Point __position = e.MouseDevice.GetPosition(grid_sl_progress);
            TimeSpan _time = new TimeSpan(0, 0, (int)__track.ValueFromPoint(__position));
            tt_sl_progress.Content = _time.ToString(TIME_FORMAT);
            tt_sl_progress.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            tt_sl_progress.HorizontalOffset = __position.X - 27;
            tt_sl_progress.VerticalOffset = __position.Y - 25;

            /* makes slider thumb follow mouse */
            double __thumb_width = __track.Thumb.Width;
            if (e.LeftButton == MouseButtonState.Pressed) {
                double __d = __position.X / (sl_progress.ActualWidth);
                double __p = sl_progress.Maximum * __d + __thumb_width * __d - (__thumb_width / 2);
                sl_progress.Value = __p;
            }
        }
        #endregion // Slider Progress
        #endregion // Sliders

        #region ToolbarButtons
        private void tbtn_play_Click(object sender, EventArgs e) {
            _play(false);
        }

        private void tbtn_stop_Click(object sender, EventArgs e) {
            _stop();
        }

        private void tbtn_pause_Click(object sender, EventArgs e) {
            _pause();
        }

        private void tbtn_prev_Click(object sender, EventArgs e) {
            _prev();
        }

        private void tbtn_next_Click(object sender, EventArgs e) {
            _next(false);
        }
        #endregion //ToolbarButtons

        #endregion // eventhandlers

        private void NOT_IMPLEMENTED_YET() {
            MessageBox.Show("Feature/Function not implement yet", "Under construction", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

		private void main_context_view_expanded_Checked(object sender, RoutedEventArgs e) {
			if (!_is_expanded) {
				_expand();
			}
		}

		private void main_context_view_expanded_Unchecked(object sender, RoutedEventArgs e) {
			if (_is_expanded) {
				_contract();
			}
		}

		private void main_context_view_fullscreen_Checked(object sender, RoutedEventArgs e) {
			if (!_is_fullscreen) {
				_fullscreen_enter();
			}
		}

		private void main_context_view_fullscreen_Unchecked(object sender, RoutedEventArgs e) {
			if (_is_fullscreen) {
				_fullscreen_leave();
			}
		}
    }
}
