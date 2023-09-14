namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Momentum Trend")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45299-momentum-trend")]
	public class MomentumTrend : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _downSeries = new("DownSeries", Strings.Down)
		{
			Color = DefaultColors.Red.Convert(),
			VisualType = VisualMode.Dots,
			Width = 3
		};
		private readonly ValueDataSeries _upSeries = new("UpSeries", Strings.Up)
		{
			Color = DefaultColors.Green.Convert(),
			VisualType = VisualMode.Dots,
			Width = 3
		};

		private readonly Momentum _momentum = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Order = 20)]
		[Range(1, 10000)]
		public int Period
		{
			get => _momentum.Period;
			set
			{
				_momentum.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MomentumTrend()
		{
			DenyToChangePanel = true;
			
			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_momentum.Calculate(bar, value);

			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				return;
			}

			var candle = GetCandle(bar);

			if (_momentum[bar] > _momentum[bar - 1])
				_upSeries[bar] = candle.High;
			else
				_downSeries[bar] = candle.Low;
		}

		#endregion
	}
}