namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("Woodies CCI")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/8470-woodies-cci")]
	public class WoodiesCCI : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _cciSeries = new("CCI")
		{
			VisualType = VisualMode.Histogram, 
			ShowCurrentValue = false, 
			Width = 2
		};
		
		private readonly CCI _entryCci = new() { Name = "Entry CCI" };

		private readonly ValueDataSeries _lsmaSeries = new("LSMA")
		{
			VisualType = VisualMode.Block, 
			ShowCurrentValue = false, 
			ScaleIt = false, 
			Width = 2, 
			IgnoredByAlerts = true,
			ShowTooltip = false
		};
		
		private readonly CCI _trendCci = new() { Name = "Trend CCI" };

		private LineSeries _line100 = new("100")
		{
			Color = Colors.Gray,
			LineDashStyle = LineDashStyle.Dash,
			Value = 100,
			Width = 1,
			IsHidden = true
		};

		private LineSeries _line200 = new("200")
		{
			Color = Colors.Gray,
			LineDashStyle = LineDashStyle.Dash,
			Value = 200,
			Width = 1,
			IsHidden = true
        };

		private LineSeries _line300 = new("300")
		{
			Color = Colors.Gray,
			LineDashStyle = LineDashStyle.Dash,
			Value = 300,
			Width = 1,
			IsHidden = true,
			UseScale = true
		};

		private LineSeries _lineM100 = new("-100")
		{
			Color = Colors.Gray,
			LineDashStyle = LineDashStyle.Dash,
			Value = -100,
			Width = 1,
			IsHidden = true
        };

		private LineSeries _lineM200 = new("-200")
		{
			Color = Colors.Gray,
			LineDashStyle = LineDashStyle.Dash,
			Value = -200,
			Width = 1,
			IsHidden = true
        };

		private LineSeries _lineM300 = new("-300")
		{
			Color = Colors.Gray,
			LineDashStyle = LineDashStyle.Dash,
			Value = -300,
			Width = 1,
			UseScale = true,
			IsHidden = true
        };

		private bool _drawLines = true;

		private int _lsmaPeriod = 25;

		private int _trendPeriod = 5;

		private int _trendUp, _trendDown;
		private System.Drawing.Color _trendUpColor = DefaultColors.Blue;
		private System.Drawing.Color _trendDownColor = DefaultColors.Maroon;
		private System.Drawing.Color _noTrendColor = DefaultColors.Gray;
		private System.Drawing.Color _timeBarColor = DefaultColors.Yellow;
		private System.Drawing.Color _positiveLsmaColor = DefaultColors.Green;
		private System.Drawing.Color _negativeLsmaColor = DefaultColors.Red;

		#endregion

        #region Properties

        [Display(Name = "CCI Trend Up", GroupName = "Drawing", Order = 610)]
        public System.Windows.Media.Color TrendUpColor
        {
	        get => _trendUpColor.Convert();
	        set
	        {
		        _trendUpColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "CCI Trend Down", GroupName = "Drawing", Order = 620)]
        public System.Windows.Media.Color TrendDownColor
        {
	        get => _trendDownColor.Convert();
	        set
	        {
		        _trendDownColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "No Trend", GroupName = "Drawing", Order = 630)]
        public System.Windows.Media.Color NoTrendColor
        {
	        get => _noTrendColor.Convert();
	        set
	        {
		        _noTrendColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "Time Bar", GroupName = "Drawing", Order = 640)]
        public System.Windows.Media.Color TimeBarColor
        {
	        get => _timeBarColor.Convert();
	        set
	        {
		        _timeBarColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "Negative LSMA", GroupName = "Drawing", Order = 650)]
        public System.Windows.Media.Color NegativeLsmaColor
        {
	        get => _negativeLsmaColor.Convert();
	        set
	        {
		        _negativeLsmaColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "Positive LSMA", GroupName = "Drawing", Order = 660)]
        public System.Windows.Media.Color PositiveLsmaColor
        {
	        get => _positiveLsmaColor.Convert();
	        set
	        {
		        _positiveLsmaColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Parameter]
		[Display(Name = "LSMA Period",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
		public int LSMAPeriod
		{
			get => _lsmaPeriod;
			set
			{
				_lsmaPeriod = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(Name = "Trend Period",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
        public int TrendPeriod
		{
			get => _trendPeriod;
			set
			{
				_trendPeriod = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(Name = "Trend CCI Period",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
        public int TrendCCIPeriod
		{
			get => _trendCci.Period;
			set
			{
				_trendCci.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(Name = "Entry CCI Period",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
        public int EntryCCIPeriod
		{
			get => _entryCci.Period;
			set
			{
				_entryCci.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "Line",
			Order = 30)]
		public bool DrawLines
		{
			get => _drawLines;
			set
			{
				_drawLines = value;

				if (value)
				{
					if (LineSeries.Any())
						return;

					LineSeries.Add(_line100);
					LineSeries.Add(_line200);
					LineSeries.Add(_line300);
					LineSeries.Add(_lineM100);
					LineSeries.Add(_lineM200);
					LineSeries.Add(_lineM300);
				}
				else
				{
					LineSeries.Clear();
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "p300",
			GroupName = "Line",
			Order = 40)]
		public LineSeries Line300
		{
			get => _line300;
			set => _line300 = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "p200",
			GroupName = "Line",
			Order = 50)]
		public LineSeries Line200
		{
			get => _line200;
			set => _line200 = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "p100",
			GroupName = "Line",
			Order = 60)]
		public LineSeries Line100
		{
			get => _line100;
			set => _line100 = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "m100",
			GroupName = "Line",
			Order = 60)]
		public LineSeries LineM100
		{
			get => _lineM100;
			set => _lineM100 = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "m200",
			GroupName = "Line",
			Order = 60)]
		public LineSeries LineM200
		{
			get => _lineM200;
			set => _lineM200 = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "m300",
			GroupName = "Line",
			Order = 60)]
		public LineSeries LineM300
		{
			get => _lineM300;
			set => _lineM300 = value;
		}
		

        #endregion

        #region ctor

        public WoodiesCCI()
			: base(true)
		{
			TrendCCIPeriod = 14;
			EntryCCIPeriod = 6;
			_trendCci.DataSeries[0].Name = "Trend CCI";
			_entryCci.DataSeries[0].Name = "Entry CCI";
			Panel = IndicatorDataProvider.NewPanel;
			((ValueDataSeries)_entryCci.DataSeries[0]).Color = DefaultColors.Orange.Convert();
			_entryCci.DataSeries[0].IgnoredByAlerts = true;
			((ValueDataSeries)_trendCci.DataSeries[0]).Width = 2;
			((ValueDataSeries)_trendCci.DataSeries[0]).Color = DefaultColors.Purple.Convert();
			_trendCci.DataSeries[0].IgnoredByAlerts = true;

			DataSeries.Add(_cciSeries);

			DataSeries.Add(_trendCci.DataSeries[0]);
			DataSeries.Add(_entryCci.DataSeries[0]);

			DataSeries.Add(_lsmaSeries);
			
			((ValueDataSeries)DataSeries[0]).ShowCurrentValue = false;
			((ValueDataSeries)DataSeries[0]).Name = "Zero Line";
			((ValueDataSeries)DataSeries[0]).Color = Colors.Gray;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
			DataSeries[0].IgnoredByAlerts = true;

			LineSeries.Add(new LineSeries("100")
			{
				Color = Colors.Gray,
				LineDashStyle = LineDashStyle.Dash,
				Value = 100,
				Width = 1
			});

			LineSeries.Add(new LineSeries("200")
			{
				Color = Colors.Gray,
				LineDashStyle = LineDashStyle.Dash,
				Value = 200,
				Width = 1
			});

			LineSeries.Add(new LineSeries("300")
			{
				Color = Colors.Gray,
				LineDashStyle = LineDashStyle.Dash,
				Value = 300,
				Width = 1,
				UseScale = true
			});

			LineSeries.Add(new LineSeries("-100")
			{
				Color = Colors.Gray,
				LineDashStyle = LineDashStyle.Dash,
				Value = -100,
				Width = 1
			});

			LineSeries.Add(new LineSeries("-200")
			{
				Color = Colors.Gray,
				LineDashStyle = LineDashStyle.Dash,
				Value = -200,
				Width = 1
			});

			LineSeries.Add(new LineSeries("-300")
			{
				Color = Colors.Gray,
				LineDashStyle = LineDashStyle.Dash,
				Value = -300,
				Width = 1,
				UseScale = true
			});

			Add(_trendCci);
			Add(_entryCci);
		}

		#endregion

		#region Protected methods
		
		protected override void OnCalculate(int bar, decimal value)
		{
			try
			{
				this[bar] = 0;

				if (_trendCci[bar] > 0 && _trendCci[bar - 1] < 0)
				{
					if (_trendDown > TrendPeriod)
						_trendUp = 0;
				}

				_cciSeries[bar] = _trendCci[bar];

				if (_trendCci[bar] > 0)
				{
					if (_trendUp < TrendPeriod)
					{
						_cciSeries.Colors[bar] = _noTrendColor;
						_trendUp++;
					}

					if (_trendUp == TrendPeriod)
					{
						_cciSeries.Colors[bar] = _timeBarColor;
                        _trendUp++;
					}

					if (_trendUp > TrendPeriod)
						_cciSeries.Colors[bar] = _trendUpColor;
                }

				if (_trendCci[bar] < 0 && _trendCci[bar - 1] > 0)
				{
					if (_trendUp > TrendPeriod)
						_trendDown = 0;
				}

				if (_trendCci[bar] < 0)
				{
					if (_trendDown < TrendPeriod)
					{
						_cciSeries.Colors[bar] = _noTrendColor;
                        _trendDown++;
					}

					if (_trendDown == TrendPeriod)
					{
						_cciSeries.Colors[bar] = _timeBarColor;
                        _trendDown++;
					}

					if (_trendDown > TrendPeriod)
						_cciSeries.Colors[bar] = _trendDownColor;
                }

				decimal summ = 0;

				if (bar < LSMAPeriod + 2)
					return;

				var lengthvar = (decimal)((LSMAPeriod + 1) / 3.0);

				for (var i = LSMAPeriod; i >= 1; i--)
					summ += (i - lengthvar) * GetCandle(bar - LSMAPeriod + i).Close;

				var wt = summ * 6 / (LSMAPeriod * (LSMAPeriod + 1));
				_lsmaSeries[bar] = 0.00001m;

				_lsmaSeries.Colors[bar] = wt > GetCandle(bar).Close 
					? _negativeLsmaColor
					: _positiveLsmaColor;
			}
			catch (Exception)
			{
			}
		}
		
		#endregion
	}
}