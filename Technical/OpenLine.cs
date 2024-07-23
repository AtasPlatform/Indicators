namespace ATAS.Indicators.Technical
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context;
    using OFT.Rendering.Settings;
    using OFT.Rendering.Tools;

    [DisplayName("Open Line")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.OpenLineDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602440")]
	public class OpenLine : Indicator
	{
		#region Nested types

		public class Session
		{
			#region Properties

			public int StartBar { get; set; }

			public int EndBar { get; set; }

			public decimal OpenPrice { get; set; }

			public bool Touched { get; set; }

			#endregion
		}

		#endregion

		#region Fields

		private int _days = 5;

        private RenderFont _font = new("Arial", 8);
		private int _fontSize = 8;
		private int _lastBar;

		private List<Session> _sessions = new();

		private int _targetBar;
		private bool _tillTouch;
		private string _openCandleText = "Open Line";
        private FilterTimeSpan _customSessionStartFilter;
        private Session _lastSession;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Days),
			GroupName = nameof(Strings.Settings), Description = nameof(Strings.DaysLookBackDescription),
            Order = 5)]
        [Range(0, 10000)]
		public int Days
		{
			get => _days;
			set
			{
				_days = value;
				RecalculateValues();
			}
		}

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSessionStart),
           GroupName = nameof(Strings.Settings), Description = nameof(Strings.CustomSessionStartFilterDescription),
           Order = 10)]
        public FilterTimeSpan CustomSessionStartFilter
		{
			get => _customSessionStartFilter;
			set => SetTrackedProperty(ref _customSessionStartFilter, value, _ =>
			{
				RecalculateValues();
				RedrawChart();
			});
		}

        #region Hidden

        [Obsolete]
		[Browsable(false)]
		public bool CustomSessionStart
		{
			get => _customSessionStartFilter.Enabled;
			set => _customSessionStartFilter.Enabled = value;

        }

        [Obsolete]
        [Browsable(false)]
		public TimeSpan StartDate
		{
            get => _customSessionStartFilter.Value;
            set => _customSessionStartFilter.Value = value;
        }

        #endregion

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text),
			GroupName = nameof(Strings.Drawing), Description = nameof(Strings.LabelTextDescription),
            Order = 30)]
		public string OpenCandleText
		{
			get => _openCandleText;
			set
			{
				if(value.Length > 1000)
					return;

				_openCandleText = value;
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextSize),
			GroupName = nameof(Strings.Drawing), Description = nameof(Strings.FontSizeDescription),
            Order = 40)]
		[Range(1, 200)]
		public int FontSize
		{
			get => _fontSize;
			set
			{
				_fontSize = value;
				_font = new RenderFont("Arial", value);
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetY),
			GroupName = nameof(Strings.Drawing), Description = nameof(Strings.LabelOffsetYDescription),
            Order = 50)]
		public int Offset { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.OpenLine),
			GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PenSettingsDescription),
            Order = 60)]
		public PenSettings LinePen { get; set; } = new() { Color = System.Drawing.Color.SkyBlue.Convert(), Width = 2 };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineTillTouch),
			GroupName = nameof(Strings.Drawing), Description = nameof(Strings.IsLineTillTouchDescription),
            Order = 62)]
		public bool TillTouch
		{
			get => _tillTouch;
			set
			{
				_tillTouch = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public OpenLine()
			: base(true)
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

			DenyToChangePanel = true;
			CustomSessionStartFilter = new(true) { Value = new(9, 0, 0) };
        }

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
            foreach (var session in _sessions)
            {
                if (session.StartBar > LastVisibleBarNumber)
                    continue;

                var x1 = ChartInfo.GetXByBar(session.StartBar, false);

                var x2 = ChartInfo.GetXByBar(session.EndBar, false);

                if (x2 < 0)
                    continue;

                var y = ChartInfo.GetYByPrice(session.OpenPrice, false);

                context.DrawLine(LinePen.RenderObject, x1, y, x2, y);

                var stringSize = context.MeasureString(OpenCandleText, _font);
                context.DrawString(OpenCandleText, _font, LinePen.RenderObject.Color, x2 - stringSize.Width, y - stringSize.Height - Offset - 3);
            }
        }

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_sessions.Clear();
				_lastSession = null;

                _targetBar = 0;

				if (_days > 0)
				{
					var days = 0;

					for (var i = CurrentBar - 1; i >= 0; i--)
					{
						_targetBar = i;

						if (!IsNewSession(i))
							continue;

						days++;

						if (days == _days)
							break;
					}
				}
			}

			if (bar < _targetBar)
				return;

            var candle = GetCandle(bar);

            if (_lastBar != bar)
            {
                _lastBar = bar;

                if (_lastSession is not null && !_lastSession.Touched)
                    _lastSession.EndBar = bar;

                if (_customSessionStartFilter.Enabled)
				{
                    var filter = _customSessionStartFilter.Value;
                    var time = candle
                        .Time.AddHours(InstrumentInfo.TimeZone)
                        .TimeOfDay;

					if (time == filter)
					{
						AddNewSession(bar, candle);
					}
					else if (bar > 0) 
                    {
                        var prevCandle = GetCandle(bar - 1);
                        var prevTime = prevCandle
                            .Time.AddHours(InstrumentInfo.TimeZone)
                            .TimeOfDay;

                        if (prevTime < time )
						{
                            if (time > filter && prevTime < filter)
							{
                                if (_lastSession != null)
                                    _lastSession.EndBar -= 1;

                                AddNewSession(bar - 1, prevCandle);								
                            }
                        }
                        else if (prevTime > time)
						{
							if((time < filter && prevTime < filter) || (time > filter && prevTime > filter))
							{
                                if (_lastSession != null)
                                    _lastSession.EndBar -= 1;

                                AddNewSession(bar - 1, prevCandle);
                            }
						}
                    }					                 
                }
				else if (IsNewSession(bar))
                {
                    AddNewSession(bar, candle);
                }
            }

			if (_lastSession is null || _lastSession.StartBar == bar)
				return;

            if (TillTouch && !_lastSession.Touched)
            {
                var open = _lastSession.OpenPrice;

                if (candle.High >= open && candle.Low <= open)
                {
                    _lastSession.Touched = true;
                    _lastSession.EndBar = bar;
                }
            }
        }

        #endregion

        #region Private methods

        private void AddNewSession(int bar, IndicatorCandle candle)
        {
            _lastSession = new Session
            {
                StartBar = bar,
                EndBar = bar,
                OpenPrice = candle.Open
            };

            _sessions.Add(_lastSession);
        }

        #endregion
    }
}