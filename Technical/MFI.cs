﻿namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
	
    [DisplayName("Money Flow Index")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MFIDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602430")]
	public class MFI : Indicator
	{
		#region Fields

		private LineSeries _overbought = new("Overbought", Strings.Overbought)
		{
			Color = DefaultColors.Green.Convert(),
			Value = 80,
			IsHidden = true
        };
		private LineSeries _oversold = new("Oversold", Strings.Oversold)
		{
			Color = DefaultColors.Green.Convert(),
			Value = 20,
			IsHidden = true
        };
		private ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
		{
			VisualType = VisualMode.Histogram,
            ShowZeroValue = false
		};
		
        private ValueDataSeries _negativeFlow = new("NegFlow");
        private ValueDataSeries _positiveFlow = new("PosFlow");

        private int _lastBar = -1;
        private int _period = 14;
        private decimal _previousTypical;
        private bool _drawLines = true;
        private System.Drawing.Color _greenColor = DefaultColors.Green;
        private System.Drawing.Color _sitColor = DefaultColors.DarkRed;
        private System.Drawing.Color _fakeColor = System.Drawing.Color.DodgerBlue;
        private System.Drawing.Color _weakColor = System.Drawing.Color.Gray;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 20)]
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
		
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.GreenSeriesColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.StrongPositiveFlowColorDescription), Order = 200)]
		public CrossColor GreenColor
		{
			get => _greenColor.Convert();
			set
			{
				_greenColor = value.Convert(); 
				RecalculateValues();
			}
		}
		
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.WeakSeriesColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.WeakNegativeFlowColorDescription), Order = 210)]
		public CrossColor WeakColor
		{
			get => _weakColor.Convert();
			set
			{
                _weakColor = value.Convert();
				RecalculateValues();
			}
        }
		
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FakeSeriesColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.WeakPositiveFlowColorDescription), Order = 220)]
		public CrossColor FakeColor
		{
			get => _fakeColor.Convert();
			set
			{
				_fakeColor = value.Convert();
				RecalculateValues();
			}
        }
		
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SitSeriesColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.StrongNegativeFlowColorDescription), Order = 230)]
		public CrossColor SitColor
		{
			get => _sitColor.Convert();
			set
			{
				_sitColor = value.Convert();
				RecalculateValues();
			}
        }
		
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.Line), Description = nameof(Strings.DrawLinesDescription), Order = 300)]
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

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Overbought), GroupName = nameof(Strings.Line), Description = nameof(Strings.OverboughtLimitDescription), Order = 310)]
		public LineSeries OverboughtLine 
		{ 
			get => _overbought;
			set => _overbought = value;
		} 

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Oversold), GroupName = nameof(Strings.Line), Description = nameof(Strings.OversoldLimitDescription), Order = 320)]
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