document.addEventListener("DOMContentLoaded", function () {

    // 1. Initialize Bootstrap Tooltips
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipTriggerList.forEach(tooltipTriggerEl => {
        new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // 2. Automatically dismiss Alert banners after 5 seconds
    const alertList = document.querySelectorAll('.alert-dismissible');
    alertList.forEach(alert => {
        setTimeout(() => {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 5000);
    });

});

/**
 * Helper function to display Toast / Alert notifications across the application
 * @param {string} message Notification content
 * @param {string} type 'success' | 'danger' | 'warning' | 'info'
 */
function showToast(message, type = 'success') {
    const alertPlaceholder = document.getElementById('toastContainer');
    if (!alertPlaceholder) return;

    const wrapper = document.createElement('div');
    wrapper.innerHTML = [
        `<div class="alert alert-${type} alert-dismissible fade show shadow-sm" role="alert">`,
        `   <div>${message}</div>`,
        '   <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>',
        '</div>'
    ].join('');

    alertPlaceholder.append(wrapper);
}