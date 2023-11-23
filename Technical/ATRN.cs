namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("ATR Normalized")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ATRNDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602633")]
public class ATRN : Indicator
{
	#region Fields

	private readonly ATR _atr = new()
	{
		Period = 10
	};

	private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

    #endregion

    #region Properties

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
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