(function()
{
	window.createNamespace = function(namespace)
	{
		var namespaces = namespace.split(".");

		var name = namespaces[0];
		window[name] = window[name] ? window[name] : {};
		var parent = window[name];

		for (var i = 1; i < namespaces.length; i++)
		{
			name = namespaces[i];

			if (!parent[name])
			{
				parent[name] = {};
			}
			parent = parent[name];
		}
		return parent;
	};
})();