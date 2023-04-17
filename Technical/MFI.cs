namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("Money Flow Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38174-money-flow-index")]
	public class MFI : Indicator
	{
		#region Fields

		private LineSeries _overbought = new(Resources.Overbought)
		{
			Color = DefaultColors.Green.Convert(),
			Value = 80,
			IsHidden = true
        };
		private LineSeries _oversold = new(Resources.Oversold)
		{
			Color = DefaultColors.Green.Convert(),
			Value = 20,
			IsHidden = true
        };
		private ValueDataSeries _renderSeries = new(Resources.Visualization)
		{
			VisualType = VisualMode.Histogram,
            ShowZeroValue = false
		};
		private int _lastBar = -1;
        private ValueDataSeries _negativeFlow = new("NegFlow");

		private int _period = 14;
        private ValueDataSeries _positiveFlow = new("PosFlow");
		private decimal _previousTypical;
        private bool _drawLines = true;
        private System.Drawing.Color _greenColor = DefaultColors.Green;
        private System.Drawing.Color _sitColor = DefaultColors.DarkRed;
        private System.Drawing.Color _fakeColor = System.Drawing.Color.DodgerBlue;
        private System.Drawing.Color _weakColor = System.Drawing.Color.Gray;

        #endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 20)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				_previousTypical = -1;
				RecalculateValues();
			}
		}
		
		[Display(ResourceType = typeof(Resources), Name = "GreenSeriesColor", GroupName = "Visualization", Order = 200)]
		public Color GreenColor
		{
			get => _greenColor.Convert();
			set
			{
				_greenColor = value.Convert(); 
				RecalculateValues();
			}
		}
		
		[Display(ResourceType = typeof(Resources), Name = "WeakSeriesColor", GroupName = "Visualization", Order = 210)]
		public Color WeakColor
		{
			get => _weakColor.Convert();
			set
			{
                _weakColor = value.Convert();
				RecalculateValues();
			}
        }
		
		[Display(ResourceType = typeof(Resources), Name = "FakeSeriesColor", GroupName = "Visualization", Order = 220)]
		public Color FakeColor
		{
			get => _fakeColor.Convert();
			set
			{
				_fakeColor = value.Convert();
				RecalculateValues();
			}
        }
		
		[Display(ResourceType = typeof(Resources), Name = "SitSeriesColor", GroupName = "Visualization", Order = 230)]
		public Color SitColor
		{
			get => _sitColor.Convert();
			set
			{
				_sitColor = value.Convert();
				RecalculateValues();
			}
        }
		
		[Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "Line", Order = 300)]
		public bool DrawLines
		{
			get => _drawLines;
			set
			{
				_drawLines = value;

				if (value)
				{
					if (LineSeries.Contains(_overbought))
						return;

					LineSeries.Add(_overbought);
					LineSeries.Add(_oversold);
				}
				else
				{
					LineSeries.Clear();
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Overbought", GroupName = "Line", Order = 310)]
		public LineSeries OverboughtLine 
		{ 
			get => _overbought;
			set => _overbought = value;
		} 

		[Display(ResourceType = typeof(Resources), Name = "Oversold", GroupName = "Line", Order = 320)]
		public LineSeries OversoldLine
        { 
			get => _oversold;
			set => _oversold = value;
		} 
        #endregion

        #region ctor

        public MFI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;
			
			DataSeries[0] = _renderSeries;

			LineSeries.Add(_overbought);
			LineSeries.Add(_oversold);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			var typical = (candle.High + candle.Low + candle.Close) / 3.0m;

			if (bar == 0)
				_previousTypical = typical;

			var moneyFlow = typical * candle.Volume;

			if (typical > _previousTypical)
				_positiveFlow[bar] = moneyFlow;
			else
				_negativeFlow[bar] = moneyFlow;

			DataSeries.ForEach(x => ((ValueDataSeries)x)[bar] = 0);

			if (bar < Period)
				return;

			var positiveFlow = _positiveFlow.CalcSum(Period, Math.Max(bar - Period, 0));
			var negativeFlow = _negativeFlow.CalcSum(Period, Math.Max(bar - Period, 0));

			var renderValue = 100m;

			if (negativeFlow != 0.0m)
			{
				var moneyRatio = positiveFlow / negativeFlow;
				renderValue = 100.0m - 100.0m / (1.0m + moneyRatio);
			}

			if (bar != _lastBar)
				_previousTypical = typical;

			_lastBar = bar;

			_renderSeries[bar] = renderValue;

            if (bar == 0)
	            return;

            var prevCandle = GetCandle(bar - 1);

            _renderSeries.Colors[bar] = renderValue >= _renderSeries[bar - 1]
	            ? candle.Ticks >= prevCandle.Ticks
		            ? _greenColor
		            : _fakeColor
	            : candle.Ticks >= prevCandle.Ticks
		            ? _sitColor
		            : _weakColor;
		}

		#endregion
	}
}