﻿namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("MACD Leader")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MacdLeaderDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602419")]
	public class MacdLeader : Indicator
	{
		#region Fields

		private readonly EMA _longEma = new() { Period = 26 };
        private readonly EMA _longEmaSmooth = new() { Period = 26 };
        private readonly MACD _macd = new()
		{
			LongPeriod = 26,
			ShortPeriod = 12,
			SignalPeriod = 9
		};

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Indicator) 
		{
			Color = System.Drawing.Color.Purple.Convert(),
            DescriptionKey = nameof(Strings.BaseLineSettingsDescription)
        };

		private readonly EMA _shortEma = new() { Period = 12 };
		private readonly EMA _shortEmaSmooth = new() { Period = 12 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int MacdPeriod
		{
			get => _macd.SignalPeriod;
			set
			{
				_macd.SignalPeriod = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShortPeriodDescription), Order = 110)]
		[Range(1, 10000)]
        public int MacdShortPeriod
		{
			get => _macd.ShortPeriod;
			set
			{
				_macd.ShortPeriod = _shortEma.Period = _shortEmaSmooth.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.LongPeriodDescription), Order = 120)]
		[Range(1, 10000)]
        public int MacdLongPeriod
		{
			get => _macd.LongPeriod;
			set
			{
				_macd.LongPeriod = _longEma.Period = _longEmaSmooth.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MacdLeader()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _renderSeries;
			DataSeries.Add(_macd.DataSeries[2]);
		}

		#endregion

		#region Protected methods
		
		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}
		
		protected override void OnCalculate(int bar, decimal value)
		{
			var macd = _macd.Calculate(bar, value);

			_shortEma.Calculate(bar, value);
			_shortEmaSmooth.Calculate(bar, value - _shortEma[bar]);
			_longEma.Calculate(bar, value);
			_longEmaSmooth.Calculate(bar, value - _longEma[bar]);

			_renderSeries[bar] = _macd[bar] + _shortEmaSmooth[bar] - _longEmaSmooth[bar];
		}

		#endregion
		
	}
}