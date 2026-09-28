namespace LoadKit.Core.Scenarios.Templates;

/// <summary>Produces one template value. Arguments are already parsed; no parsing happens here.</summary>
public delegate string TemplateValueFactory(TemplateContext context);
