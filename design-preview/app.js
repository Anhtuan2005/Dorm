(function () {
  var params = new URLSearchParams(window.location.search);
  var screen = params.get('screen') || 'dashboard';
  var allowed = ['dashboard', 'rooms', 'student', 'applications', 'billing', 'maintenance'];
  if (allowed.indexOf(screen) === -1) screen = 'dashboard';
  document.body.dataset.screen = screen;
  window.scrollTo(0, 0);

  document.querySelectorAll('[data-nav]').forEach(function (item) {
    item.classList.toggle('active', item.dataset.nav === screen);
  });

  document.querySelectorAll('[data-demo-toast]').forEach(function (button) {
    button.addEventListener('click', function () {
      var toast = document.getElementById('demo-toast');
      toast.querySelector('b').textContent = button.dataset.demoToast;
      toast.classList.add('visible');
      window.setTimeout(function () { toast.classList.remove('visible'); }, 2600);
    });
  });
})();
