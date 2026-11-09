// Confirm dialogs for destructive forms: <form data-confirm="Are you sure?">
document.addEventListener('submit', function (e) {
    var message = e.target && e.target.dataset ? e.target.dataset.confirm : null;
    if (message && !window.confirm(message)) {
        e.preventDefault();
    }
});

// Registration: only show the invite code field when "Organiser" is chosen
(function () {
    var role = document.getElementById('Role');
    var group = document.getElementById('inviteGroup');
    if (!role || !group) return;

    function toggle() {
        group.style.display = role.value === 'Organiser' ? '' : 'none';
    }
    role.addEventListener('change', toggle);
    toggle();
})();
