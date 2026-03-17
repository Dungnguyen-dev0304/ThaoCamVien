document.addEventListener('DOMContentLoaded', function () {
    // 1. Khai báo các thành phần
    const tabLogin = document.getElementById('tab-login');
    const tabRegister = document.getElementById('tab-register');
    const loginForm = document.getElementById('login-form');
    const registerForm = document.getElementById('register-form');

    // 2. Hàm kiểm tra định dạng Email (Regex)
    function isValidEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(email);
    }

    // 3. Xử lý chuyển đổi Tab
    function switchTab(type) {
        if (type === 'login') {
            tabLogin.classList.add('active');
            tabRegister.classList.remove('active');
            loginForm.classList.add('active');
            registerForm.classList.remove('active');
        } else {
            tabRegister.classList.add('active');
            tabLogin.classList.remove('active');
            registerForm.classList.add('active');
            loginForm.classList.remove('active');
        }
    }

    tabLogin.addEventListener('click', () => switchTab('login'));
    tabRegister.addEventListener('click', () => switchTab('register'));

    // 4. Xử lý Đăng nhập (Kiểm tra Database)
    loginForm.addEventListener('submit', async function (e) {
        e.preventDefault();
        const username = document.getElementById('login-user').value;
        const password = document.getElementById('login-pass').value;

        try {
            // Gửi yêu cầu kiểm tra đến API
            const response = await fetch('/api/account/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username: username, password: password })
            });

            if (response.ok) {
                const data = await response.json();
                alert("Đăng nhập thành công! Chào mừng " + data.name);
                // Sau khi đăng nhập xong, chuyển về trang chủ (index.html cùng cấp)
                window.location.href = "index.html";
            } else {
                const error = await response.text();
                alert("Lỗi: " + (error || "Sai tài khoản hoặc mật khẩu!"));
            }
        } catch (err) {
            alert("Không thể kết nối với máy chủ API. Hãy kiểm tra Backend!");
        }
    });

    // 5. Xử lý Đăng ký (Lưu vào Database)
    registerForm.addEventListener('submit', async function (e) {
        e.preventDefault();

        const nameInput = document.getElementById('reg-name');
        const emailInput = document.getElementById('reg-email');
        const passInput = document.getElementById('reg-pass');
        const emailError = document.getElementById('email-error');
        const passError = document.getElementById('pass-error');

        let isValid = true;

        // Validation phía Client (như cũ)
        if (!isValidEmail(emailInput.value)) {
            emailInput.classList.add('invalid');
            emailError.style.display = 'block';
            isValid = false;
        } else {
            emailInput.classList.remove('invalid');
            emailError.style.display = 'none';
        }

        if (passInput.value.length < 8) {
            passInput.classList.add('invalid');
            passError.style.display = 'block';
            isValid = false;
        } else {
            passInput.classList.remove('invalid');
            passError.style.display = 'none';
        }

        // Nếu hợp lệ, gửi dữ liệu lên API để lưu vào SQL Server
        if (isValid) {
            const userData = {
                name: nameInput.value,
                email: emailInput.value,
                password: passInput.value
            };

            try {
                const response = await fetch('/api/account/register', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(userData)
                });

                if (response.ok) {
                    alert("Chúc mừng " + userData.name + "! Bạn đã đăng ký thành công.");
                    switchTab('login'); // Tự động quay về form đăng nhập
                } else {
                    const errorMsg = await response.text();
                    alert("Lỗi đăng ký: " + errorMsg);
                }
            } catch (err) {
                alert("Lỗi kết nối API!");
            }
        }
    });
});