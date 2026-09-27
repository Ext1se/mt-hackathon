using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Scenarios.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// The ticket terminal (MMT) the conductor carries, as a device screen sliding up from the bottom: a status bar with
    /// the story clock, a "Seats" tab (a car picker, the picked car's seat map and the picked seat's passenger record) and
    /// a "Route" tab (stops, and where the train is now). It opens on the conductor's own car.
    /// </summary>
    public sealed class TerminalView : MonoBehaviour
    {
        private const string RouteSeparator = " \u2014 ";
        private const string TitleSeparator = " \u00b7 ";

        [SerializeField] private GameObject _root;
        [Tooltip("The device body; slides up when the terminal opens.")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;
        [SerializeField] private TMP_Text _clock;
        [SerializeField] private Button _closeButton;
        [Tooltip("Shown next to the close button for a seat the scenario wants studied (e.g. 3B); leads to the conclusion.")]
        [SerializeField] private Button _studyButton;

        [Header("Tabs")]
        [SerializeField] private Button _seatsTab;
        [SerializeField] private Button _routeTab;
        [SerializeField] private GameObject _seatsPage;
        [SerializeField] private GameObject _routePage;
        [SerializeField] private Color _activeTabColor = new Color(0.90f, 0.25f, 0.20f, 1f);
        [SerializeField] private Color _inactiveTabColor = new Color(0.17f, 0.23f, 0.32f, 1f);

        [Header("Seats")]
        [Tooltip("Parent of the car buttons (a horizontal layout).")]
        [SerializeField] private RectTransform _carPicker;
        [Tooltip("Inactive car button with an Image and a text; cloned once per car.")]
        [SerializeField] private Button _carTemplate;
        [Tooltip("Parent of the map rows (a vertical layout, pivot at the top); shrunk to fit short screens.")]
        [SerializeField] private RectTransform _map;
        [Tooltip("Inactive horizontal layout cloned once per map row.")]
        [SerializeField] private GameObject _rowTemplate;
        [Tooltip("Inactive seat button with an Image and a text; cloned once per seat.")]
        [SerializeField] private Button _seatTemplate;
        [Tooltip("Inactive text in the aisle: the row number, or empty in the letters row.")]
        [SerializeField] private TMP_Text _aisleTemplate;
        [SerializeField] private TMP_Text _detailsTitle;
        [SerializeField] private TMP_Text _detailsBody;

        [Header("Route")]
        [Tooltip("Parent of the stops and the stretches between them (a vertical layout).")]
        [SerializeField] private RectTransform _routeList;
        [Tooltip("Inactive stop row: a dot Image, then the name and time texts.")]
        [SerializeField] private GameObject _stationTemplate;
        [Tooltip("Inactive stretch row: a line Image, then the status text.")]
        [SerializeField] private GameObject _stretchTemplate;

        [Header("Labels")]
        [Tooltip("Details title; {0} is the seat id.")]
        [SerializeField] private string _seatFormat = "{0}";
        [Tooltip("Details text of a seat without a ticket.")]
        [SerializeField] private string _freeText = string.Empty;
        [SerializeField] private string _passengerLabel = string.Empty;
        [SerializeField] private string _tripLabel = string.Empty;
        [SerializeField] private string _nameLabel = string.Empty;
        [SerializeField] private string _birthDateLabel = string.Empty;
        [SerializeField] private string _documentLabel = string.Empty;
        [SerializeField] private string _phoneLabel = string.Empty;
        [SerializeField] private string _ticketLabel = string.Empty;
        [SerializeField] private string _routeLabel = string.Empty;
        [SerializeField] private string _departureLabel = string.Empty;
        [SerializeField] private string _arrivalLabel = string.Empty;
        [SerializeField] private string _tariffLabel = string.Empty;
        [SerializeField] private string _baggageLabel = string.Empty;
        [SerializeField] private string _statusLabel = string.Empty;
        [SerializeField] private string _noteLabel = string.Empty;

        [Header("Colours")]
        [SerializeField] private Color _soldColor = new Color(0.20f, 0.42f, 0.66f, 1f);
        [SerializeField] private Color _freeColor = new Color(0.20f, 0.23f, 0.29f, 1f);
        [SerializeField] private Color _selectedColor = new Color(0.90f, 0.25f, 0.20f, 1f);
        [SerializeField] private Color _labelColor = new Color(0.62f, 0.67f, 0.75f, 1f);
        [SerializeField] private Color _headingColor = new Color(0.90f, 0.25f, 0.20f, 1f);
        [SerializeField] private Color _alertColor = new Color(0.98f, 0.62f, 0.22f, 1f);
        [SerializeField] private Color _nowColor = new Color(0.98f, 0.78f, 0.22f, 1f);
        [Tooltip("Where the value column starts, in percent of the details width.")]
        [SerializeField, Range(10f, 60f)] private float _valueIndentPercent = 34f;

        [Header("Opening")]
        [SerializeField, Range(0f, 1f)] private float _slideSeconds = 0.22f;
        [SerializeField, Range(0f, 600f)] private float _slideDistance = 260f;

        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<Button> _carButtons = new List<Button>();
        private readonly List<GameObject> _routeRows = new List<GameObject>();
        private readonly Dictionary<string, Image> _seatImages = new Dictionary<string, Image>();
        private readonly StringBuilder _builder = new StringBuilder();
        private TerminalData _data;
        private TerminalCarData _car;
        private Func<TerminalSeatData, TerminalRecordData> _resolveRecord;
        private string _selectedSeat;
        private bool _isRouteShown;
        private Vector2 _panelHome;
        private Coroutine _slide;

        /// <summary>The player opened a sold seat; the runner may offer to study it.</summary>
        public event Action<TerminalSeatData> SeatOpened;

        /// <summary>The study button was pressed for the open seat; the runner applies the seat's bound option.</summary>
        public event Action<TerminalSeatData> StudyRequested;

        /// <summary>The close button was pressed; the runner decides how the terminal closes.</summary>
        public event Action CloseRequested;

        public bool IsShown => _root.activeSelf;

        private void Awake()
        {
            _rowTemplate.SetActive(false);
            _carTemplate.gameObject.SetActive(false);
            _seatTemplate.gameObject.SetActive(false);
            _aisleTemplate.gameObject.SetActive(false);
            _stationTemplate.SetActive(false);
            _stretchTemplate.SetActive(false);
            _panelHome = _panel.anchoredPosition;
            _closeButton.onClick.AddListener(OnCloseClicked);
            if (_studyButton != null)
            {
                _studyButton.onClick.AddListener(OnStudyClicked);
                _studyButton.gameObject.SetActive(false);
            }

            _seatsTab.onClick.AddListener(OnSeatsTabClicked);
            _routeTab.onClick.AddListener(OnRouteTabClicked);
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            _closeButton.onClick.RemoveListener(OnCloseClicked);
            if (_studyButton != null)
            {
                _studyButton.onClick.RemoveListener(OnStudyClicked);
            }

            _seatsTab.onClick.RemoveListener(OnSeatsTabClicked);
            _routeTab.onClick.RemoveListener(OnRouteTabClicked);
        }

        /// <summary>
        /// Shows <paramref name="data"/>; <paramref name="resolveRecord"/> gives a sold seat's record for the current
        /// scenario state, or null when none applies. Opening starts on the own car and the seats tab; showing it again
        /// while open, e.g. after a seat check was recorded, keeps the tab, the car and the picked seat.
        /// </summary>
        public void Show(TerminalData data, Func<TerminalSeatData, TerminalRecordData> resolveRecord)
        {
            bool isOpening = !_root.activeSelf;
            _resolveRecord = resolveRecord;
            _root.SetActive(true);
            if (data != _data)
            {
                _data = data;
                _car = null;
                BuildCarPicker();
                BuildRoute();
            }

            if (isOpening || _car == null)
            {
                _isRouteShown = false;
                SelectCar(data.DefaultCar);
            }

            _subtitle.text = data.Subtitle;
            _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(data.Subtitle));
            _clock.text = data.Clock;
            _routeTab.gameObject.SetActive(data.Route != null);
            ShowTitle();
            ShowDetails();
            PaintSeats();
            ShowPage();
            Deselect();

            if (isOpening)
            {
                SlideIn();
            }
        }

        public void Hide()
        {
            if (_slide != null)
            {
                StopCoroutine(_slide);
                _slide = null;
            }

            _panel.anchoredPosition = _panelHome;
            _root.SetActive(false);
            _selectedSeat = null;
            SetStudyAvailable(false);
        }

        /// <summary>Shows or hides the study button for the seat that is open now.</summary>
        public void SetStudyAvailable(bool isAvailable)
        {
            if (_studyButton != null)
            {
                _studyButton.gameObject.SetActive(isAvailable);
            }
        }

        private static void Deselect()
        {
            EventSystem current = EventSystem.current;
            if (current != null)
            {
                current.SetSelectedGameObject(null);
            }
        }

        private static void Paint(Button button, Color color)
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        private void OnCloseClicked()
        {
            Deselect();
            CloseRequested?.Invoke();
        }

        private void OnStudyClicked()
        {
            Deselect();
            TerminalSeatData seat = string.IsNullOrEmpty(_selectedSeat) || _car == null ? null : _car.FindSeat(_selectedSeat);
            if (seat != null)
            {
                StudyRequested?.Invoke(seat);
            }
        }

        private void OnSeatsTabClicked()
        {
            _isRouteShown = false;
            ShowPage();
            Deselect();
        }

        private void OnRouteTabClicked()
        {
            _isRouteShown = true;
            ShowPage();
            Deselect();
        }

        private void ShowPage()
        {
            bool isRoute = _isRouteShown && _data.Route != null;
            _seatsPage.SetActive(!isRoute);
            _routePage.SetActive(isRoute);
            Paint(_seatsTab, isRoute ? _inactiveTabColor : _activeTabColor);
            Paint(_routeTab, isRoute ? _activeTabColor : _inactiveTabColor);
            if (!isRoute)
            {
                FitMap();
            }
        }

        // The map keeps its seat size where it fits and shrinks as a whole on short screens (portrait, small windows).
        private void FitMap()
        {
            _map.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_map);
            Canvas.ForceUpdateCanvases();
            float height = LayoutUtility.GetPreferredHeight(_map);
            float width = LayoutUtility.GetPreferredWidth(_map);
            Rect area = _map.rect;
            if (height <= 0f || width <= 0f || area.height <= 0f)
            {
                return;
            }

            float scale = Mathf.Min(1f, area.height / height, area.width / width);
            _map.localScale = new Vector3(scale, scale, 1f);
        }

        private void SlideIn()
        {
            if (_slide != null)
            {
                StopCoroutine(_slide);
            }

            _slide = StartCoroutine(SlideRoutine());
        }

        private IEnumerator SlideRoutine()
        {
            float elapsed = 0f;
            while (elapsed < _slideSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / _slideSeconds), 3f);
                _panel.anchoredPosition = _panelHome + new Vector2(0f, -_slideDistance * (1f - progress));
                yield return null;
            }

            _panel.anchoredPosition = _panelHome;
            _slide = null;
        }

        private void BuildCarPicker()
        {
            for (int i = 0; i < _carButtons.Count; i++)
            {
                _carButtons[i].gameObject.SetActive(false);
                Destroy(_carButtons[i].gameObject);
            }

            _carButtons.Clear();
            for (int i = 0; i < _data.Cars.Count; i++)
            {
                TerminalCarData car = _data.Cars[i];
                Button button = Instantiate(_carTemplate, _carPicker);
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TMP_Text>(true).text = car.Id;
                button.onClick.AddListener(() => OnCarClicked(car));
                _carButtons.Add(button);
            }
        }

        private void OnCarClicked(TerminalCarData car)
        {
            Deselect();
            if (car == _car)
            {
                return;
            }

            SelectCar(car);
            ShowTitle();
            ShowDetails();
            PaintSeats();
            FitMap();
        }

        // Another car is another map; the picked seat belongs to the old one and is dropped.
        private void SelectCar(TerminalCarData car)
        {
            _car = car;
            _selectedSeat = null;
            SetStudyAvailable(false);
            BuildMap();
            for (int i = 0; i < _carButtons.Count; i++)
            {
                Paint(_carButtons[i], _data.Cars[i] == _car ? _activeTabColor : _inactiveTabColor);
            }
        }

        private void ShowTitle()
        {
            _title.text = _car == null || string.IsNullOrEmpty(_car.Title) ? _data.Title : _data.Title + TitleSeparator + _car.Title;
        }

        private void BuildMap()
        {
            // Destroy waits for the end of the frame; switched off now, the old rows drop out of the layout and the fit.
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].SetActive(false);
                Destroy(_rows[i]);
            }

            _rows.Clear();
            _seatImages.Clear();
            if (_car == null)
            {
                return;
            }

            GameObject letters = AddMapRow();
            for (int column = 0; column < _car.Columns.Count; column++)
            {
                AddAisleIfDue(letters.transform, column, string.Empty);
                TMP_Text letter = Instantiate(_aisleTemplate, letters.transform);
                letter.text = _car.Columns[column];
                letter.gameObject.SetActive(true);
            }

            for (int row = 1; row <= _car.Rows; row++)
            {
                GameObject line = AddMapRow();
                for (int column = 0; column < _car.Columns.Count; column++)
                {
                    AddAisleIfDue(line.transform, column, row.ToString(CultureInfo.InvariantCulture));
                    AddSeat(line.transform, TerminalData.SeatId(row, _car.Columns[column]));
                }
            }
        }

        private GameObject AddMapRow()
        {
            GameObject row = Instantiate(_rowTemplate, _map);
            row.SetActive(true);
            _rows.Add(row);
            return row;
        }

        private void AddAisleIfDue(Transform row, int column, string text)
        {
            if (column == _car.AisleAfter)
            {
                TMP_Text aisle = Instantiate(_aisleTemplate, row);
                aisle.text = text;
                aisle.gameObject.SetActive(true);
            }
        }

        private void AddSeat(Transform row, string seatId)
        {
            Button seat = Instantiate(_seatTemplate, row);
            seat.gameObject.SetActive(true);
            seat.GetComponentInChildren<TMP_Text>(true).text = seatId;
            seat.onClick.AddListener(() => OnSeatClicked(seatId));
            _seatImages[seatId] = seat.GetComponent<Image>();
        }

        // Stops and the stretches between them; the stretch after the last passed stop carries "the train is here".
        private void BuildRoute()
        {
            for (int i = 0; i < _routeRows.Count; i++)
            {
                _routeRows[i].SetActive(false);
                Destroy(_routeRows[i]);
            }

            _routeRows.Clear();
            TerminalRouteData route = _data.Route;
            if (route == null)
            {
                return;
            }

            int lastPassed = route.LastPassedIndex;
            for (int i = 0; i < route.Stations.Count; i++)
            {
                TerminalStationData station = route.Stations[i];
                GameObject stop = Instantiate(_stationTemplate, _routeList);
                stop.SetActive(true);
                _routeRows.Add(stop);
                stop.GetComponentInChildren<Image>(true).color = station.Passed ? _soldColor : _freeColor;
                TMP_Text[] texts = stop.GetComponentsInChildren<TMP_Text>(true);
                texts[0].text = station.Name;
                texts[1].text = station.Time;

                if (i == route.Stations.Count - 1)
                {
                    break;
                }

                bool isNow = i == lastPassed;
                GameObject stretch = Instantiate(_stretchTemplate, _routeList);
                stretch.SetActive(true);
                _routeRows.Add(stretch);
                stretch.GetComponentInChildren<Image>(true).color = isNow ? _nowColor : i < lastPassed ? _soldColor : _freeColor;
                TMP_Text status = stretch.GetComponentInChildren<TMP_Text>(true);
                status.text = isNow ? route.Status : string.Empty;
                status.color = _nowColor;
            }
        }

        private void OnSeatClicked(string seatId)
        {
            Deselect();
            _selectedSeat = seatId;
            ShowDetails();
            PaintSeats();
            SetStudyAvailable(false);

            TerminalSeatData seat = _car.FindSeat(seatId);
            if (seat != null && FindRecord(seat) != null)
            {
                SeatOpened?.Invoke(seat);
            }
        }

        private void PaintSeats()
        {
            foreach (KeyValuePair<string, Image> pair in _seatImages)
            {
                Color color = _freeColor;
                if (pair.Key == _selectedSeat)
                {
                    color = _selectedColor;
                }
                else
                {
                    TerminalSeatData seat = _car.FindSeat(pair.Key);
                    if (seat != null && FindRecord(seat) != null)
                    {
                        color = _soldColor;
                    }
                }

                pair.Value.color = color;
            }
        }

        private TerminalRecordData FindRecord(TerminalSeatData seat)
        {
            return _resolveRecord != null ? _resolveRecord(seat) : null;
        }

        private void ShowDetails()
        {
            if (string.IsNullOrEmpty(_selectedSeat))
            {
                _detailsTitle.gameObject.SetActive(false);
                _detailsBody.text = _data.Prompt;
                return;
            }

            _detailsTitle.gameObject.SetActive(true);
            _detailsTitle.text = string.Format(_seatFormat, _selectedSeat);
            TerminalSeatData seat = _car.FindSeat(_selectedSeat);
            TerminalRecordData record = seat != null ? FindRecord(seat) : null;
            _detailsBody.text = record != null ? BuildRecord(record) : _freeText;
        }

        private string BuildRecord(TerminalRecordData record)
        {
            _builder.Clear();
            AppendHeading(_passengerLabel);
            AppendField(_nameLabel, record.Name, false);
            AppendField(_birthDateLabel, record.BirthDate, false);
            AppendField(_documentLabel, record.Document, false);
            AppendField(_phoneLabel, record.Phone, false);

            _builder.Append('\n');
            AppendHeading(_tripLabel);
            AppendField(_ticketLabel, record.Ticket, false);
            string route = string.IsNullOrEmpty(record.From) || string.IsNullOrEmpty(record.To)
                ? record.From + record.To
                : record.From + RouteSeparator + record.To;
            AppendField(_routeLabel, route, false);
            AppendField(_departureLabel, record.Departure, false);
            AppendField(_arrivalLabel, record.Arrival, false);
            AppendField(_tariffLabel, record.Tariff, false);
            AppendField(_baggageLabel, record.Baggage, false);
            AppendField(_statusLabel, record.Status, record.Alert);
            AppendField(_noteLabel, record.Note, false);
            return _builder.ToString();
        }

        private void AppendHeading(string heading)
        {
            _builder.Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(_headingColor)).Append('>')
                .Append(heading).Append("</color></b>\n");
        }

        // The value column is an indent, so a long value wraps under itself rather than under the label.
        private void AppendField(string label, string value, bool isAlert)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(_labelColor)).Append('>').Append(label)
                .Append("</color><indent=").Append(_valueIndentPercent.ToString("0", CultureInfo.InvariantCulture))
                .Append("%>");
            if (isAlert)
            {
                _builder.Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(_alertColor)).Append('>').Append(value)
                    .Append("</color></b>");
            }
            else
            {
                _builder.Append(value);
            }

            _builder.Append("</indent>\n");
        }
    }
}
