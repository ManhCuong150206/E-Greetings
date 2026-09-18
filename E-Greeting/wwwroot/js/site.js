document.addEventListener("DOMContentLoaded", function () {

    // 1. Tự động highlight Menu (active state) dựa theo URL hiện tại
    const currentPath = window.location.pathname.toLowerCase();
    const navLinks = document.querySelectorAll(".nav-link-custom, .dropdown-item");

    navLinks.forEach(link => {
        const href = link.getAttribute("href");
        if (href && href !== "#" && currentPath.includes(href.toLowerCase())) {
            link.classList.add("active", "fw-bold", "text-primary");
        }
    });

    // 2. Kích hoạt Bootstrap Tooltips (nếu có dùng icon hướng dẫn)
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipTriggerList.forEach(tooltipTriggerEl => {
        new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // 3. Tự động ẩn thông báo Alert sau 5 giây (Success/Error Message)
    const alertList = document.querySelectorAll('.alert-dismissible');
    alertList.forEach(alert => {
        setTimeout(() => {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 5000);
    });

});

/**
 * Hàm hỗ trợ hiển thị thông báo Toast / Alert nhanh cho toàn ứng dụng
 * @param {string} message Nội dung thông báo
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