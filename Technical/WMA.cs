namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("WMA")]
	[LocalizedDescription(typeof(Resources), "WMA")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7211-wma")]
	public class WMA : Indicator
	{
		#region Fields

		private int _lastBar = -1;
		private int _myPeriod;
		private int _period = 10;
		private decimal _priorSum;
		private decimal _priorWsum;
		private decimal _sum;
		private decimal _wsum;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
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
		
		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar < _lastBar)
			{
				_wsum = 0;
				_sum = 0;
			}

			if (bar != _lastBar)
			{
				_lastBar = bar;
				_priorWsum = _wsum;
				_priorSum = _sum;
				_myPeriod = Math.Min(bar + 1, Period);
			}

			_wsum = _priorWsum - (bar >= Period ? _priorSum : 0) + _myPeriod * value;
			_sum = _priorSum + value - (bar >= Period ? (decimal)SourceDataSeries[bar - Period] : 0);
			this[bar] = _wsum / (0.5m * _myPeriod * (_myPeriod + 1));
		}

		#endregion
	}
}