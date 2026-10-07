// client side for [NotBeforeToday]
(function ($) {
    function today() {
        const d = new Date();
        return [d.getFullYear(), String(d.getMonth() + 1).padStart(2, '0'), String(d.getDate()).padStart(2, '0')].join('-');
    }

    function other(el, field) {
        // inputs are named like "Input.ExpectedCloseDate", so look the other field up with the same prefix
        const prefix = el.name.includes('.') ? el.name.substring(0, el.name.lastIndexOf('.') + 1) : '';
        return el.form.querySelector('[name="' + prefix + field + '"]');
    }

    $.validator.addMethod('today', function (value, el, p) {
        if (!value) return true;
        if (p.field) {
            const o = other(el, p.field);
            if (o && p.values.includes(o.value)) return true;
        }
        return value.slice(0, 10) >= today();
    });

    $.validator.unobtrusive.adapters.add('today', ['field', 'values'], function (o) {
        o.rules.today = { field: o.params.field, values: (o.params.values || '').split(',') };
        o.messages.today = o.message;
    });
})(jQuery);
