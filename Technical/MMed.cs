namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Moving Median")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45294-moving-median")]
	public class MMed : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private int _period = 10;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MMed()
		{
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var startBar = Math.Max(0, bar - Period);
			var orderedValues = new List<decimal>();

			for (var i = startBar; i <= bar; i++)
				orderedValues.Add((decimal)SourceDataSeries[i]);

			orderedValues = orderedValues
				.OrderBy(x => x)
				.ToList();

			if (bar < Period)
			{
				var targetBar = bar - (bar + 1) / 2;

				if ((bar + 1) % 2 == 1)
					_renderSeries[bar] = orderedValues[bar - targetBar];
				else
					_renderSeries[bar] = (orderedValues[bar - targetBar] + orderedValues[bar - (targetBar + 1)]) / 2;
			}
			else
			{
				var targetBar = bar - Period / 2;

				if (Period % 2 == 1)
					_renderSeries[bar] = orderedValues[bar - targetBar];
				else
					_renderSeries[bar] = (orderedValues[bar - targetBar] + orderedValues[bar - (targetBar + 1)]) / 2;
			}
		}

		#endregion
	}
}