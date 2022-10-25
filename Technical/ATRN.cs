namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("ATR Normalized")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43436-atr-normalized")]
public class ATRN : Indicator
{
	#region Fields

	private readonly ATR _atr = new()
	{
		Period = 10
	};

	private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
	[Range(1, 10000)]
	public int Period
	{
		get => _atr.Period;
		set
		{
			_atr.Period = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public ATRN()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		Add(_atr);
		DataSeries[0] = _renderSeries;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		_renderSeries[bar] = 100m * ((ValueDataSeries)_atr.DataSeries[0])[bar] / GetCandle(bar).Close;
	}

	#endregion
}