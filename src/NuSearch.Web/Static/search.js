$(document).ready(function () {
  $(function () {
    $("select").change(function () { $("form").submit(); });
  });

    $("#query").focus().select();

    setupTypeAhead();

});


function setupTypeAhead() {
  var typeAheadOptions = {
    hint: true,
    highlight: true,
    minLength: 1
  };

  var remoteHandler = function (query, process) {
    return $.ajax(
      {
        cache: false,
        type: "POST",
        url: "/suggest",
        data: JSON.stringify({ Query: query }),
        contentType: "application/json; charset=utf-8",
        dataType: "json"
      })
      .success(function (suggestions) { process(suggestions); });
  };

  $('#query').typeahead(typeAheadOptions, {
    displayKey: "id",
    templates: {
      empty: [
        '<div class="lead">',
        'unable to find any packages',
        '</div>'
      ].join('\n'),
      suggestion: function (suggestion) {
        return [
          '<h3 class="text-primary">',
          suggestion.id,
          '<span class="label label-default label-lg">',
          suggestion.downloadCount + " downloads",
          '</span>',
          '</h3>',
          '<h4 class="text-primary">',
          suggestion.summary,
          '</h4>'


        ].join('\n');
      }
    },
    source: remoteHandler
  }
  ).on('typeahead:selected', function (e, o) {
    window.location.href = "https://www.nuget.org/packages/" + o.id;
  });
}