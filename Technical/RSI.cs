namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Settings;

	using Utils.Common.Attributes;
	using Utils.Common.Localization;

	[DisplayName("RSI")]
	[LocalizedDescription(typeof(Resources), "RSI")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/7085-rsi")]
	public class RSI : Indicator
	{
		#region Fields

		private readonly SMMA _negative;
		private readonly SMMA _positive;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _positive.Period;
			set
			{
				if (value <= 0)
					return;

				_positive.Period = _negative.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public RSI()
		{
			Panel = IndicatorDataProvider.NewPanel;

			LineSeries.Add(new LineSeries("Down")
			{
				Color = Colors.Orange,
				LineDashStyle = LineDashStyle.Dash,
				Value = 30,
				Width = 1
			});
			LineSeries.Add(new LineSeries("Up")
			{
				Color = Colors.Orange,
				LineDashStyle = LineDashStyle.Dash,
				Value = 70,
				Width = 1
			});

			_positive = new SMMA();
			_negative = new SMMA();

			Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				this[bar] = 0;
			else
			{
				var diff = (decimal)SourceDataSeries[bar] - (decimal)SourceDataSeries[bar - 1];
				var pos = _positive.Calculate(bar, diff > 0 ? diff : 0);
				var neg = _negative.Calculate(bar, diff < 0 ? -diff : 0);

				if (neg != 0)
				{
					var div = pos / neg;

					this[bar] = div == 1
						? 0m
						: 100m - 100m / (1m + div);
				}
				else
					this[bar] = 100m;
			}
		}

		#endregion
	}
}