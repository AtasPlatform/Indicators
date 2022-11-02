namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Simple Moving Average - Skip Zeros")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45282-simple-moving-average-skip-zeros")]
	public class SZMA : Indicator
	{
		#region Fields

		private int _period = 10;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion
		
		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var sum = 0m;
			var nonZeroValues = 0;

			for (var i = Math.Max(0, bar - _period); i <= bar; i++)
			{
				if ((decimal)SourceDataSeries[i] == 0)
					continue;

				sum += (decimal)SourceDataSeries[i];
				nonZeroValues++;
			}

			this[bar] = nonZeroValues != 0 
				? sum / nonZeroValues
				: 0;
		}

		#endregion
	}
}