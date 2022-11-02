namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Welles Wilders Moving Average")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45291-welles-wilders-moving-average")]
	public class WWMA : Indicator
	{
		#region Fields
		
		private readonly SZMA _szma = new() { Period = 10 };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _szma.Period;
			set
			{
				_szma.Period = value;
				RecalculateValues();
			}
		}

		#endregion
		
		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_szma.Calculate(bar, value);

			if (bar == 0)
			{
				this[bar] = value;
				return;
			}
			
			this[bar] = this[bar - 1] == 0
				? _szma[bar]
				: this[bar - 1] + (value - this[bar - 1]) / Period;
		}

		#endregion
	}
}